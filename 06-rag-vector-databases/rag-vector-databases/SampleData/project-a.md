# Cedar delivery service

## Container delivery

Mira, a fictional developer, used Docker to package an ASP.NET Core parcel tracking API. She wrote the Dockerfile, built a container image in the build pipeline, and ran that image in the team's test environment. The same image carried the API and its configuration defaults between environments.

## Request handling

Mira added request validation and consistent error responses to the parcel lookup endpoints. Integration checks sent valid and invalid tracking numbers to the test API. The project description makes no claim about managing a cluster or running production infrastructure.
