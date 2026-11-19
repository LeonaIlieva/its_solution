# Implementation notes

I had ideas for how to structure the application and handle premium calculation and auditing. I talked them through with AI and made a few adjustments, but kept the overall approach. This helped me decide on the main pieces before coding and avoid reworking them later.

## Premium calculation

`Claims/Domain/PremiumCalculator.cs` calculates the premium using a daily rate and three day ranges. The base rate is 1,250, adjusted by cover type: Yacht +10%, Passenger Ship +20%, Tanker +50%, and other types +30%.

Days 1–30 use the full rate. Days 31–180 get a 5% Yacht or 2% other-type discount. From day 181 onward the total discount is 8% for Yacht and 3% for other types. Each range is priced separately, so the later discount does not change the price of earlier days. A partial day counts as a full day. Discussing the rule helped me settle on this progressive interpretation and express it as clear day bands.

## Asynchronous auditing

Audit events are put in a bounded in-memory queue and written to SQL Server by a background worker. This keeps the audit insert off the HTTP request path. The claim or cover database operation itself still happens during the request; only the separate audit write is queued. If the queue is full, the request waits for space rather than losing an event.

I chose an in-memory queue because it is simple to run and test, and the task allows it. A broker such as Azure Service Bus could also do this, but would add setup and operational work for this application. The trade-off is that queued events can be lost if the process crashes, and the business write and audit write are not one transaction. Graceful shutdown tries to drain the queue. If audit records must never be lost, a durable broker or transactional outbox would be a better fit.

Delete events record attempts, even if the item is missing or a cover cannot be deleted because a claim refers to it. The cover check and delete are separate operations, so a concurrent claim could still race with deletion.

## Other implementation choices

I added application services and repository interfaces to separate the controllers from database access. I considered CQRS and MediatR, but the API has simple CRUD operations and no separate read models or complex workflows. Adding handlers and a mediator would add layers without much benefit here.

A few supporting changes were needed to make the flow work: request DTOs prevent clients from setting generated IDs or premiums; `Program.cs` registers the new services and audit worker; and an EF migration updates the audit schema. I kept the existing MongoDB, SQL Server, and Testcontainers setup rather than changing the infrastructure. The application uses folders for its layers instead of separate projects, which keeps the solution small but does not enforce dependency direction at build time.

## SOLID

The code follows several SOLID ideas, but not all of them strictly. Responsibilities are separated between controllers, services, repositories, and the premium calculator, and services depend on repository interfaces. There are places to improve: for example, adding a new cover type's rate means changing the calculator, and the folder-based layers are not enforced by project boundaries. I chose not to add more abstractions for this small application; doing so would add indirection without enough benefit and make the code harder to follow. I would revisit those choices if the rules or application grow.
