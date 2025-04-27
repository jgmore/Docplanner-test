# Docplanner Test Solution 📄

## Overview

This repository contains a .NET 9 Web API project built to solve the Docplanner coding challenge.

The project provides a **REST API** that:
- Retrieves available doctor slots by week.
- Allows patients to book available slots.
- Abstracts communication with the external Slot Service.
- Protects endpoints using **JWT authentication**.

---

## Instructions

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (Preview or latest stable)
- [Visual Studio 2022/2025](https://visualstudio.microsoft.com/) (with .NET and Web Development workload)
- Internet access (to reach the external Slot Service API)
- [Git](https://git-scm.com/) (for cloning the repository)

### How to Run

1. **Clone the repository:**
   ```bash
   git clone https://github.com/jgmore/Docplanner-test.git
   cd Docplanner-test
   ```

2. **Restore dependencies:**
   ```bash
   cd Docplanner.BackendTest
   dotnet restore
   ```

3. **Create `.env` files:**
-In the `Docplanner.API` directory inside `Docplanner.BackendTest` folder, create a `.env` file with the following environment variables:

   ```env
   SlotApi__Username=techuser
   SlotApi__Password=secretpassWord
   Jwt__Key=ThisIsASuperSecretKeyForJWTSigning123!
   AUTH_USERS=user1:pass1,user2:pass2,user3:pass3
   ```
   
-In the `Docplanner.Tests` directory inside `Docplanner-test` folder, create a `.env` file with the following environment variables:

   ```env
   SlotApi__Username=user
   SlotApi__Password=passWord
   AUTH_USERS=user1:pass1,user2:pass2,user3:pass3
   ```
   
4. **Run the Web API:**
   ```bash
   dotnet run --project Docplanner.API
   ```

5. **Access Swagger UI:**
   Open your browser and navigate to:
   ```
   https://localhost:<port>/swagger
   ```
   (Replace `<port>` with the one shown in the console output. By default it should be 5001)

6. **Authentication:**
   - You will need a valid JWT token to access secured endpoints.
   - Use `/api/Auth/login` to **fetch the authentication token**.
   - Click on Authorize and enter "Bearer JWT_token_obtained", for example "Bearer eyJhbGciOiJIUzI1NiI...lKI", then press Authorize button.

7. **Test the API:**
   - Use `/api/Slots/week/{monday}` to **fetch available slots**.
   - Use `/api/Slots/book` to **book a selected slot**.

---

## API Endpoints

### Fetch Weekly Availability

```bash
GET /api/Slots/week/{monday}
```
- `monday` format: `yyyyMMdd` (must be a Monday)
- Returns available slots for the week.

### Book a Slot

```bash
POST /api/Slots/book
```
Request Body Example:
```json
{
  "facilityId":"a6882e6c-cf3d-40a4-93d8-4584894fc539",
  "start": "2024-11-25 10:00:00",
  "end": "2024-11-25 10:20:00",
  "comments": "Severe headache",
  "patient": {
    "name": "John",
    "secondName": "Doe",
    "email": "john.doe@example.com",
    "phone": "123-456-7890"
  }
}
```

---

## Observations

- Authentication credentials are stored in a environment variable for simplicity, this can be implemented in different ways for a more robust application, from having the credentials in a database to have a connection to another API where the credentials are checked or even both for two force authentication.

- Some tests take a long time to run because of the retry functionality.


---

## Additional Thoughts

- The project is using Retry to connect to the external Slot Service, in a situation where the Slot Service is not stable we can implement a fallback functionality where we persist the data (for example storing the results of the GET in a database and update it with the POST requests) and have the requests that were unsuccesfull because network connectivity stored, and then have some programmed activity that send the missed requests on certain times.

- In above case, we could implement an Optimistic Concurrency Control (for example adding a timestamp to the record) to avoid conflicts at commit time when booking a slot locally.

- **Health Checks and Metrics**:  
  For production-grade environments, **health check endpoints** (e.g., `/health`) and **metrics endpoints** (e.g., `/metrics`) could be added.  
  - **Health checks** would monitor the availability of external dependencies like the Slot Service, database connections (if any), and the API itself.  
  - **Metrics** could provide observability into API usage, performance, and error rates, for example, using libraries like **Prometheus-net** or **OpenTelemetry**.  
  - This would allow better monitoring, quicker detection of outages, and proactive system management.

---

## Project Structure

| Project | Description |
|:--------|:------------|
| `Docplanner.API` | ASP.NET Core Web API exposing endpoints for slot management. |
| `Docplanner.Application` | Application services coordinating logic between API and Infrastructure. |
| `Docplanner.Infrastructure` | Communication with the external Slot Service (HTTP client + settings). |
| `Docplanner.Domain` | Core domain models and business logic definitions. |
| `Docplanner.Tests` | Unit tests validating application services and controllers. |

---

# 📚 Patterns Used in This Solution

| Pattern | Usage |
|:---|:---|
| **Dependency Injection** | Used everywhere through constructor injection |
| **Repository Pattern** (lightweight) | Abstraction over availability and booking services |
| **Service Layer Pattern** | `SlotService` acts as the service orchestrating business logic |
| **Options Pattern** | `SlotServiceOptions` and binding configuration |
| **Factory Pattern** (via `HttpClientFactory`) | Creating configured HttpClients for the external Slot Service |
| **Retry Pattern** (via Polly) | Retrying external API failures |
| **Cache Aside Pattern** | Data is loaded into MemoryCache only when needed (on-demand caching) |
| **Middleware Pattern** | Custom `ErrorHandlingMiddleware` for consistent error handling |
| **DTO Pattern** | Data Transfer Objects separate API layer from domain models |
| **Logging Pattern** | Standardized logging using `ILogger<T>` |

---

# 📋 **Summary of the Patterns Used**

| Category | Patterns |
|:---|:---|
| Dependency Management | Dependency Injection, Options Pattern |
| Communication/Resiliency | Retry Pattern (Polly), Factory Pattern (HttpClientFactory) |
| Business Logic Layer | Service Layer Pattern, DTO Pattern |
| Infrastructure | Repository Pattern (lightweight abstraction) |
| Performance | Cache Aside Pattern (MemoryCache) |
| API and Middleware | Middleware Pattern (ErrorHandlingMiddleware) |
| Observability | Logging Pattern |

---
