# Fictional projects: Mira Example

These six project records are invented teaching data, not a real person's resume.

## [P1] PostgreSQL inventory service

Mira built an inventory service for a fictional community workshop using C# and ASP.NET Core.
PostgreSQL stores stock levels and product records. The service exposes endpoints for receiving
supplies and recording their use. Database transactions prevent two requests from consuming
the same stock. Mira added integration checks for concurrent updates and documented the API
for the workshop's volunteer maintainers.

## [P2] TypeScript event website

Mira created a fictional neighborhood event website using TypeScript and React. Event descriptions
are stored in checked-in JSON files. The site filters events by date and venue, and its layout
adapts to phones and desktop screens. Keyboard navigation and descriptive form labels make
the registration form easier to use. The project has no server-side database.

## [P3] Kubernetes telemetry collector

Mira developed a fictional telemetry collector in Go. Kubernetes runs the collector as a deployment
with health checks and a resource limit. Prometheus scrapes counters for accepted and rejected
events. The collector forwards validated events to an external monitoring service. Mira wrote
deployment instructions and tested graceful shutdown so queued events can finish before a
container exits.

## [P4] SQLite reading journal

Mira made a fictional desktop reading journal using C# and WPF. SQLite stores book titles,
reading sessions, and personal notes in a local file. Users can search their entries and export
them as CSV. The journal works without a network connection. Mira added a confirmation before
deleting an entry and a small migration for adding reading-session tags.

## [P5] Redis notification worker

Mira built a fictional notification worker using C# and ASP.NET Core. Redis tracks deduplication
keys so repeated events do not produce duplicate notifications. The worker reads messages from
a queue and sends status updates to a test mail service. Mira implemented a bounded retry
for temporary delivery failures and a dead-letter path for messages that cannot be processed.

## [P6] CSV spending report

Mira created a fictional spending report tool using C# and .NET. The tool reads CSV expense
records, validates dates and amounts, and groups totals by category. It writes a compact HTML
report containing a table of monthly totals. Input and output files stay on the user's computer.
Mira added clear messages for missing columns and invalid amounts so users can correct their
source files.
