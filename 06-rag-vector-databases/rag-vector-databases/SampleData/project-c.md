# Harbor inventory prototype

## Local development

Mira used Docker Compose to start a PostgreSQL database beside a small C# stock API for local development. She documented how to initialize fictional inventory data and tested database migrations from an empty volume. This was a local prototype, with no measured production traffic.

## Stock history

Each stock adjustment recorded a product ID, quantity change, and reason. Mira wrote tests for concurrent adjustments and showed missing products as explicit errors. She did not report sales forecasts or financial results for this fictional project.
