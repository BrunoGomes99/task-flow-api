# TaskFlow — Phase 3 Due Reminder Design

Date: 2026-10-16  
Status: **Implemented** in the repository. EC2 runtime is confirmed when the stack is applied and the images are published. Design approved 2026-10-16.

Supersedes the Phase 3 messaging scope that published `TaskCreated` and `TaskCompleted` so a consumer could insert `NotificationLog`. Cache behavior stays in [2026-10-14-phase3-cache-design.md](2026-10-14-phase3-cache-design.md).

## Goal

Send one real email about 24 hours before a task's `DueDate`, using RabbitMQ and the Brevo free plan. `NotificationLog` is the receipt that this reminder was sent for that task and that due instant.

The flow runs in local Compose and on the current EC2 Compose host. ECS is a later host for the same two worker processes, not part of this phase.

## Non-goals

- Emails on task create, update, or complete.
- Publishing `TaskCreated` or `TaskCompleted`.
- An HTTP API to list notification logs.
- Outbox. The log is written after Brevo accepts the message, not before publish.
- RabbitMQ repair of the Redis cache.
- ECS, Fargate, ALB, or moving Mongo off the EC2 host.
- A second consumer replica, a dedicated MongoDB, or a distributed Redis topology.
- Catch-up for tasks whose 24-hour window already passed, including tasks created with less than 23 hours until `DueDate`.
- A custom domain or DNS authentication at Brevo. Sender verification of one address is enough.
- Mailpit or another local capture server. Local Compose sends through Brevo.

## Decisions (Approved)

### D1 — Reminder, not create/complete

The only message is `DueSoon`. `NotificationEventType` replaces `TaskCreated` and `TaskCompleted` with `DueSoon`. Those two values were never published. Domain tests that mention them change with the enum.

Create, update, and status changes stay synchronous HTTP use cases. They do not publish.

### D2 — Only tasks with a due date

`DueDate` stays optional. The scheduler ignores tasks where it is null. A task with a due date that is later cleared is not reminded for the cleared value.

### D3 — Two workers, API stays out

Same solution. Two new host projects, both referencing Infrastructure the same way the API does:

- `TaskFlow.Scheduler` scans and publishes.
- `TaskFlow.Notifications` consumes, sends mail, and writes the log.

The API process does not connect to RabbitMQ. Use cases and interfaces live in Application. RabbitMQ types, the Brevo client, and MongoDB repositories live in Infrastructure. Hosts only compose those pieces.

One scheduler process. On EC2 that is one container. The split exists so a later ECS service can run the scheduler at desired count 1 while the API scales separately.

### D4 — What the scheduler selects

Every **15 minutes** the scheduler loads tasks that match all of the following. It also runs once when the process starts, so a local stack does not wait for the first interval.

- `DueDate` is not null.
- Status is not `Completed`.
- `DueDate` is in the half-open UTC window `[UtcNow + 23 hours, UtcNow + 24 hours)`.
- No `NotificationLog` exists for that `TaskId` + `DueSoon` + that `DueDate`.

The window is one hour so a single missed tick does not drop the reminder. The mail goes out between 24 and 23 hours before the due instant. Comparisons use the UTC instant already stored for `DueDate`.

The scan is cross-tenant. It is an internal worker query, not a user-scoped repository call. User HTTP use cases stay filtered by the JWT `UserId`.

A task created with `DueDate` already sooner than 23 hours never enters the window. A scheduler outage longer than one hour skips the tasks whose window passed. This phase does not backfill them.

Changing `DueDate` creates a new key. An old receipt does not block the new instant. Completing a task removes it from later scans.

### D5 — Message

Application contract, no RabbitMQ types:

- `TaskId`
- `UserId`
- `DueDate` (the task's due instant)

Queue: `taskflow.due-soon`. Dead-letter queue: `taskflow.due-soon.dlq`. Consumer prefetch is **1**. Ack is manual.

### D6 — Consumer steps

For each delivery:

1. If a `NotificationLog` already exists for that `TaskId` + `DueSoon` + message `DueDate`, ack and stop. This is a redelivery of a finished send.
2. Load the task. If it is missing, `Completed`, or its `DueDate` is null or different from the message, ack and stop. Do not send and do not write a log.
3. Load the owner by `UserId`. Send the email through Brevo.
4. Insert the log. A unique index on `TaskId` + `EventType` + `DueDate` rejects a second insert. That rejection does not change the ack.
5. Ack, because Brevo accepted the mail.

### D7 — When not to ack

Ack after Brevo accepts, and when step 1 or step 2 says the message is finished.

If Brevo fails, or the owner cannot be loaded, do not insert the log and do not ack. Requeue. After **5** failed processing attempts, reject the message to `taskflow.due-soon.dlq`. Do not retry forever.

If Brevo accepts the mail and the insert fails, still ack. A missing receipt can let a later scan publish the same due instant again while it remains inside the one-hour window. Requeueing here would send again on every attempt, up to five times, and the scan could publish once more after that.

If the process dies after the insert and before ack, redelivery hits step 1 and does not send again. If it dies after Brevo accepts and before the insert, redelivery can send a second email.

### D8 — Receipt shape

`NotificationLog` gains the due instant it acknowledges:

- `Id`
- `TaskId`
- `EventType` (`DueSoon`)
- `DueDate` (copied from the message, not "now")
- `ProcessedAt` (UTC instant of the insert, after Brevo accepts)

No HTTP endpoint reads this collection. The scheduler and the consumer do.

MongoDB indexes:

- Unique `{ TaskId, EventType, DueDate }` on `NotificationLogs`.
- `{ Status, DueDate }` on `Tasks` for the scan. The existing `UserId` and `UserId + DueDate` indexes stay.

### D9 — Brevo

The free plan (300 emails per day, no expiry, no card). Both Compose stacks send to real inboxes.

Configuration, never committed:

- `Brevo__ApiKey`
- `Brevo__SenderEmail` (the verified sender)
- `Brevo__SenderName`

The message is plain text in Portuguese. It includes the task title and the due instant in UTC. The recipient is the task owner's account email.

Unit tests use a fake sender. They do not call Brevo or RabbitMQ.

### D10 — Compose on the laptop and on EC2

Add RabbitMQ, `TaskFlow.Scheduler`, and `TaskFlow.Notifications` to local `docker-compose` and to `infra/compose/docker-compose.aws.yml`.

- Workers and the broker talk on the Compose network. Do not publish AMQP `5672` on either stack.
- Local Compose may publish the RabbitMQ management UI on `15672`, same idea as Redis Commander. The AWS security group does not open `5672` or `15672`.
- On EC2, cap RabbitMQ at **256 MiB**. Redis stays at 64 MiB.
- Worker health checks hit a local `GET /health` inside the container. Those ports are not published.
- `restart: unless-stopped`.
- The API does not depend on RabbitMQ.

The study host is still one EC2 with Compose and the temporary SSM redeploy. Implementation publishes two more images (`taskflow-scheduler`, `taskflow-notifications`) to ECR, tagged `github.sha`, and the SSM script must copy the updated Compose file to `/opt/taskflow` and pull all three images. User-data does not re-run on the existing instance (`user_data_replace_on_change` is false). Replacing the instance would drop the MongoDB volume, so the running host is updated in place.

### D11 — Instance size

Keep `t3.micro` (1 GiB) if API + Mongo + Redis + capped RabbitMQ + two workers stay healthy. If that host runs out of memory, set `instance_type` to **`t3.small`** (2 GiB). That change stays on EC2 + Compose + SSM. It is not an ECS migration.

## Success criteria

- [x] `NotificationEventType` is only `DueSoon`. Create and complete do not publish.
- [x] Tasks with a null `DueDate`, and tasks already `Completed`, are not reminded.
- [x] The scheduler publishes `DueSoon` only for the UTC window in D4, every 15 minutes, and skips a task that already has a receipt for that due instant.
- [x] The consumer follows D6 and D7, including manual ack, prefetch 1, and the dead-letter queue after 5 failures.
- [x] A redelivery after a saved receipt does not send a second email.
- [x] A changed `DueDate` can produce a new reminder. The old receipt does not block it.
- [x] Brevo is wired for local Compose and the EC2 Compose host. The API key is not in git. A live send still needs the key in `.env`.
- [x] RabbitMQ, scheduler, and notifications are in both Compose files. AMQP is not published. EC2 does not expose the management UI.
- [ ] The EC2 stack stays up on `t3.micro`, or on `t3.small` if 1 GiB is not enough, still without ECS. Confirm after `terraform apply` and the SSM redeploy.
- [x] Unit tests mock the publisher, the consumer's external calls, and Brevo. They do not require RabbitMQ.
