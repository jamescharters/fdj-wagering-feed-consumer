# Wagering Stats API

## Prerequisites

This application was developed and tested on macOS. Ensure that the following are installed:

- .NET 10 (or Docker)

## Code

```bash
git clone https://github.com/jamescharters/fdj-wagering-feed-consumer.git
cd fdj-wagering-feed-consumer
```

## Configuration

Before running, set the candidate ID in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "WageringFeed": {
    "CandidateId": "value"
  }
}

```

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run
```

The API will start and begin consuming the wagering feed via the specified WebSocket.

The application runs on `http://localhost:5000` (or as configured in `launchSettings.json`).

To query a specific customer, invoke a GET (via cURL) or point your web browser to `http://localhost:5000/customer/<customerId>/stats` where `customerID` is a known customer ID number.

## API Documentation

Swagger UI is available at `http://localhost:5000/swagger` and the OpenAPI spec can be found at `http://localhost:5000/openapi/v1.json`.

## Docker

Alternatively, you can run the application using Docker:

```bash
# Build the image
docker build -t wagering-stats-api .

# Run the container (use real CANDIDATE_ID)
docker run -p 5000:8080 -e WageringFeed__CandidateId=CANDIDATE_ID wagering-stats-api
```

Or using Docker Compose:

```bash
# Set your candidate ID and run
CANDIDATE_ID=your-id-here docker compose up
```

The app will be available at `http://localhost:5000/customer/<customerId>/stats`.

## Design Notes

### Assumptions

- "This will only work for the duration of your session" is assumed to mean the API HTTP GET endpoint `/customer/<customerId>/stats` only returns a response payload while the WebSocket session itself is active. In effect, this means the API only provides HTTP 200 range responses for valid customer IDs for about 3 minutes after start. Be quick!
- TotalStandToWin in the response has no currency conversion, and decimal values rounded to 2 places (i.e. the value corresponds to dollars-and-cents, Euro-and-cents etc)
- Automatic connection to web socket to receive messages is appropriate on application start, and once EndOfFeed is received (or our own hard timelimit expires), there is no automatic reconnection or even mechanism to re-establish the websocket subscription
- "Fixture" messages don't seem to be needed for the customer stats endpoint, so they're ignored
- Repository `Clear()` only gets called at startup before the WebSocket connects, so no locking needed there

### Limitations

- In-memory data storage, therefore everything is lost on app restart
- Only works as a single instance (multiple instances would each have their own separate data, leading to potentially inconsistent responses basded on which server answers the client query)
- API returns HTTP 503 once the feed completes (dis/gracefully), which might not be ideal depending on actual real world requirements
- Web socket reconnection retries forever with a 10 second delay (there is no backoff or circuit breaker logic for this)
- No auth, rate limiting, or caching (see TODOs/DEVNOTE remarks in the code)

### Potential Extensions

- Persistent storage such as Redis so data survives restarts and permit shared state for horizontal scaling (eg. Redis as backing store)
- Health check endpoints for container orchestration
- Metrics/tracing
- Rate limiting middleware
- Auth (JWT, client API keys, or some other form)
- Use Polly library or similar for retry policies with exponential backoff
- Actual customer name lookup from a database or other appropriate source
- Secrets management for sensitive config
