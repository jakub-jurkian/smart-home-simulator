# Smart Home Simulator

> **Smart Home Simulator** is a fullstack IoT platform simulating connected home devices. It ingests device data over MQTT and TCP, pushes real-time updates to a React frontend via SignalR, and deploys to Azure through Infrastructure as Code.

[Live Demo](https://lively-pebble-072971f10.6.azurestaticapps.net) · ![CI](https://github.com/jakub-jurkian/smart-home-simulator/actions/workflows/azure-deploy.yml/badge.svg)

---

## Engineering Highlights

* **Multi-protocol communication:** The simulator publishes sensor data over MQTT; a background `MqttListenerService` consumes it and updates the database. `SignalR` pushes those updates to connected clients in real time. A separate raw TCP socket server (port `9000`) offers an alternative text-command interface (`LOGIN`, `LIST`, `TOGGLE`).
* **Clean Architecture:** Strict separation between `Domain`, `Infrastructure`, and `Api` layers. Device polymorphism (light bulbs, temperature sensors) is modeled with EF Core's Table-Per-Hierarchy (`TPH`) inheritance.
* **Docker + Azure IaC:** Multi-stage Docker builds cut image size from ~800MB (SDK) to ~200MB (runtime-only). The entire Azure environment (Container Apps, Static Web App, SQL Database, ACR) is defined declaratively in a single `Bicep` template.
* **Hybrid CI/CD:** GitHub Actions runs tests, verifies Docker builds, and deploys the Static Web App; container apps are deployed via a local Bicep + `deploy.ps1` pipeline (workaround for university-tenant restrictions on Azure Service Principals).
* **Testing:** 80%+ coverage across unit tests (`xUnit`/`Moq`), integration tests, BDD scenarios (`Reqnroll`/`Gherkin`), and load tests (`NBomber`).

---

## Tech Stack

* **Backend:** `.NET 10`, `ASP.NET Core`, `EF Core`, `MQTTnet`, `SignalR`, `Serilog`
* **Frontend:** `React 19`, `TypeScript`, `Tailwind CSS v4`
* **DevOps:** `Docker Compose`, `Azure Container Apps`, `Azure SQL Database`, `Azure Container Registry`, `Bicep`, `GitHub Actions`

---

## Getting Started

**Prerequisites:** Docker and Docker Compose installed.

```bash
# Clone the repository
git clone [https://github.com/jakub-jurkian/smart-home-simulator.git](https://github.com/jakub-jurkian/smart-home-simulator.git)
cd smart-home-simulator

# Start the full ecosystem locally
docker compose up --build
````

* **Frontend:** `http://localhost:5173`
* **API / Swagger:** `http://localhost:5000/swagger`
* **MQTT Broker:** `localhost:1883` (TCP) · `localhost:9001` (WebSockets)

---

## Testing

```bash
# Run unit, integration, and BDD suites
dotnet test
```

*NBomber load tests are run separately via:*
```bash
dotnet run --project tests/SmartHome.PerformanceTests
```

---

## Full Documentation

Detailed schemas, infrastructure setup, and the full API/TCP command reference live in [`/docs`](./docs):
* [Architecture & Azure deployment](./docs/ARCHITECTURE_AND_DEVOPS.md)
* [API reference & TCP commands](./docs/API_REFERENCE.md)

---

## License
MIT
