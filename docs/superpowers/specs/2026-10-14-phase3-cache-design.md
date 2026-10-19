# TaskFlow — Phase 3 Cache Design

Date: 2026-10-14  
Status: **Implemented** on `feature/cache-implementation`. Design approved 2026-10-14. Plan: [../plans/2026-10-14-phase3-cache.md](../plans/2026-10-14-phase3-cache.md).

## Goal

Record how Redis cache behaves for `GetTaskById` before Phase 3 coding starts: Cache Aside, a fixed lifetime, and what the API does when deleting the cache key fails.

Messaging is specified separately, and no longer as `TaskCreated` / `TaskCompleted` log writes. See [2026-10-16-phase3-due-reminder-design.md](2026-10-16-phase3-due-reminder-design.md). This cache note does not change that design.

## Non-goals

- Caching list, create, or other task reads.
- Sliding expiration.
- Rolling back a MongoDB write when Redis is unavailable.
- Returning HTTP 500 after the task update or delete has already committed, only because the cache delete failed.
- An outbox, retry queue, or RabbitMQ message whose job is to repair the cache.
- Publishing Redis port `6379` on the EC2 security group or the host. Redis stays on the Compose network, same rule as MongoDB.
- A larger EC2 instance for the cache slice. The study host stayed `t3.micro` for Redis. The due-reminder design may raise it to `t3.small` if RabbitMQ and the two workers do not fit in 1 GiB.

## Decisions (Approved)

### D1 — Cache Aside on GetTaskById

MongoDB stays the source of truth. `GetTaskById` looks up the cache first. On a miss, it loads the task from the repository and stores that result. Update and delete remove the entry for that task.

The cache key includes **UserId and TaskId**, so one user cannot read another user's task through a shared key.

Read-through, write-through, and write-behind are not used. The handler owns the read. Writes stay in Mongo first; the cache is dropped rather than updated in place.

### D2 — Redis outside the API process

The cache is Redis, as its own Compose service, not an in-memory cache inside the API. One EC2 does not need this for speed: `GetTaskById` is a small indexed read. The separate Redis store is what later API instances would share.

Phase 3 runs that service in **both** Compose files: local `docker-compose` and `infra/compose/docker-compose.aws.yml`. On the EC2 host (`t3.micro`, 1 GiB, API + Mongo already on the box) Redis is viable only with these limits:

- Do not publish `6379`. The API reaches Redis by the Compose service name.
- Cap Redis memory (`maxmemory` on the order of 64 MiB) and evict with `allkeys-lru`, so Mongo and the API keep the rest of the RAM.
- Persistence is unnecessary: entries live 60 seconds and MongoDB is the source of truth. A Redis restart is a cold cache, which D5 already treats as a miss.

### D3 — Absolute TTL of 60 seconds

Each stored entry expires **60 seconds after it is written**. Reading the key does not extend that lifetime.

Sliding expiration was rejected. If a delete of the key fails, later GETs would keep refreshing a stale task and the entry would not age out.

Five minutes was rejected for the same failure. Update and delete respond **204**, and the client learns the new state from a following GET. A failed invalidation must not leave that GET wrong for longer than a minute.

The TTL is the backstop. Freshness on the happy path still comes from deleting the key after a successful write.

### D4 — Invalidation failure

Update and delete use this order:

1. Persist the task change in MongoDB and keep that result.
2. Delete the cache key (UserId + TaskId) in the same operation, up to **three** attempts, with a short pause between them.
3. If a delete succeeds, the next GET loads MongoDB and stores the current task.
4. If all three deletes fail, log the key (UserId and TaskId) and return success for the business operation.

Until the absolute 60 second TTL elapses, GET may return the previous task, or a task that was already deleted. That window is accepted. There is no further repair in this phase.

### D5 — Redis errors on read

A Redis failure on GET is a cache miss. The handler reads MongoDB and still responds. A cache outage must not fail `GetTaskById`.

### D6 — Known race

A GET that missed the key can store the old task after a successful invalidation. Phase 3 does not add versions or locks for that race. The 60 second TTL bounds how long the old entry can remain.

## Success criteria

- [x] `GetTaskById` uses Cache Aside with a key that contains UserId and TaskId.
- [x] Entries use an absolute 60 second TTL (not sliding).
- [x] Update and delete remove that key, with up to three attempts.
- [x] After three failed deletes, the MongoDB write still succeeds, the key is logged, and the API does not respond with 500 for the cache failure.
- [x] A Redis error on read falls back to MongoDB.
- [x] Local Compose and `infra/compose/docker-compose.aws.yml` both run Redis. Port `6379` is not published. EC2 Redis uses a small `maxmemory` cap.
