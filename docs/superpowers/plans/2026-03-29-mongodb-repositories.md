# MongoDB Repositories Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the MongoDB infrastructure base for Phase 1 with concrete `UserRepository` and `TaskRepository` implementations, collection access, indexes, and DI wiring.

**Architecture:** Keep Application contracts unchanged and implement Infrastructure as a thin persistence adapter around MongoDB. Use dedicated MongoDB document models plus explicit mappers so Domain entities stay isolated from driver concerns and repository behavior remains easy to test.

**Tech Stack:** .NET 10, MongoDB.Driver, xUnit, Mongo2Go

---

### Task 1: Add infrastructure test project

**Files:**
- Create: `tests/TaskFlow.Infrastructure.Tests/TaskFlow.Infrastructure.Tests.csproj`
- Modify: `TaskFlow.slnx`
- Test: `tests/TaskFlow.Infrastructure.Tests/Repositories/`

- [ ] **Step 1: Write the failing test project and first repository test files**
- [ ] **Step 2: Run the infrastructure tests to verify they fail because the infrastructure types do not exist yet**
- [ ] **Step 3: Add the minimal package/project references required for MongoDB repository testing**
- [ ] **Step 4: Run the tests again to confirm the failure is now about missing implementation behavior**

### Task 2: Implement MongoDB persistence primitives

**Files:**
- Create: `src/TaskFlow.Infrastructure/Configuration/MongoDbSettings.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/TaskFlowMongoContext.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/Documents/UserDocument.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/Documents/TaskDocument.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/Mappers/UserDocumentMapper.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/Mappers/TaskDocumentMapper.cs`

- [ ] **Step 1: Write a failing test for domain/document round-tripping needed by repositories**
- [ ] **Step 2: Run the targeted test and verify the expected failure**
- [ ] **Step 3: Implement the minimal MongoDB settings, context, documents, and mappers**
- [ ] **Step 4: Re-run targeted tests and keep them green**

### Task 3: Implement concrete repositories

**Files:**
- Create: `src/TaskFlow.Infrastructure/Repositories/UserRepository.cs`
- Create: `src/TaskFlow.Infrastructure/Repositories/TaskRepository.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Repositories/UserRepositoryTests.cs`
- Test: `tests/TaskFlow.Infrastructure.Tests/Repositories/TaskRepositoryTests.cs`

- [ ] **Step 1: Write failing repository behavior tests for add/get/exists and paged task queries**
- [ ] **Step 2: Run targeted tests and verify the repositories fail for the expected reason**
- [ ] **Step 3: Implement minimal repository behavior to satisfy the tests**
- [ ] **Step 4: Re-run the targeted tests and keep them green**

### Task 4: Register infrastructure and indexes

**Files:**
- Create: `src/TaskFlow.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- Create: `src/TaskFlow.Infrastructure/Persistence/MongoIndexesInitializer.cs`
- Modify: `src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj`
- Modify: `src/TaskFlow.Api/Program.cs`

- [ ] **Step 1: Write a failing test for DI registration or index initialization**
- [ ] **Step 2: Run the targeted test and verify it fails**
- [ ] **Step 3: Implement DI registration, collection setup, and index initialization**
- [ ] **Step 4: Re-run tests and verify the infrastructure wiring works**

### Task 5: Verify the slice end-to-end

**Files:**
- Modify: `docs/ENGINEERING_GUIDELINES.md`

- [ ] **Step 1: Run infrastructure tests**
- [ ] **Step 2: Run the full test suite**
- [ ] **Step 3: Run the solution build**
- [ ] **Step 4: Update the engineering checklist items that are now true**
