# Jordan Vale

This is a fictional resume for a learning lab.

## Role
Backend developer at the fictional company Cedar Finch Software.

## Skills
C#, ASP.NET Core, PostgreSQL, xUnit, GitHub Actions.

## Project
Built REST APIs for a fictional inventory service using C# and ASP.NET Core.
Stored inventory records in PostgreSQL.
Added request validation and wrote xUnit unit tests.
Configured GitHub Actions to build the service and run its tests.

## Delivery work
Designed endpoints for adding stock, reserving products, and recording deliveries.
Used PostgreSQL transactions so a reservation and its stock update succeed together.
Worked with two fictional warehouse coordinators to clarify what should happen when a product is unavailable.
Returned clear validation errors for missing product codes, negative quantities, and duplicate delivery references.
Documented the API with example requests and explained the reservation rules to another developer.

## Testing and maintenance
Added xUnit tests for rejected requests, unavailable stock, and repeated delivery submissions.
Investigated a slow inventory-history query and added a PostgreSQL index after comparing its query plans.
Reviewed small changes with teammates and kept database migrations alongside the application code.
Wrote a short support guide showing how to trace a failed request without logging private customer data.

## Learning and scope
Practised Docker locally to run the API and its database together during development.
The project used fictional inventory records and was a small internal learning service.
Jordan did not manage a production cluster or measure production traffic.
