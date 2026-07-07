# AI Agent Guide

This repository is a full-stack car auction application. Use this guide as the first map before changing code.

## Quick Mental Model

- The frontend is a Next.js app in `frontend/web-app`.
- The backend is a .NET microservice solution in `src`.
- Public browser/API traffic goes through `GatewayService` on `http://localhost:6001`.
- `AuctionService` is the source of truth for auctions, stored in PostgreSQL.
- `SearchService` is a MongoDB read/search projection of auctions.
- `BiddingService` stores bids in MongoDB and publishes bid/auction-finished events.
- `NotificationService` bridges RabbitMQ events to SignalR for live UI updates.
- `IdentityService` is a Duende IdentityServer host that issues tokens for the frontend.
- `Contracts` contains the MassTransit message contracts shared by services.

## Repo Map

```text
Carsties.sln
docker-compose.yml
src/
  AuctionService/        Authoritative auction CRUD, PostgreSQL, EF Core, gRPC server
  BiddingService/        Bid API, MongoDB, gRPC client, auction finish background worker
  SearchService/         MongoDB search/read model, event consumers, bootstrap sync
  NotificationService/   SignalR hub plus RabbitMQ consumers
  GatewayService/        YARP reverse proxy and gateway auth policy
  IdentityService/       Duende IdentityServer and ASP.NET Identity users
  Contracts/             Shared event classes
frontend/
  web-app/               Next.js App Router frontend
```

## Runtime Topology

`docker-compose.yml` defines the local stack:

- PostgreSQL: `localhost:5432`
- MongoDB: `localhost:27017`
- RabbitMQ: `localhost:5672`, management UI `localhost:15672`
- IdentityService: `localhost:5001`
- GatewayService: `localhost:6001`
- AuctionService HTTP: `localhost:7001`
- AuctionService gRPC: `localhost:7777`
- SearchService: `localhost:7002`
- BiddingService: `localhost:7003`
- NotificationService: `localhost:7004`
- Frontend dev server: `localhost:3000`

The gateway public routes are configured in `src/GatewayService/appsettings.json`.

Important gateway routes:

- `GET /auctions/**` -> `AuctionService /api/auctions/**`
- `POST|PUT|DELETE /auctions/**` -> `AuctionService /api/auctions/**`, authorized
- `GET /search/**` -> `SearchService /api/search/**`
- `POST /bids` -> `BiddingService /api/bids`, authorized
- `GET /bids/**` -> `BiddingService /api/bids/**`
- `/notifications/**` -> `NotificationService` SignalR hub

## Backend Services

### AuctionService

Main files:

- `src/AuctionService/Program.cs`
- `src/AuctionService/Controllers/AuctionsController.cs`
- `src/AuctionService/Data/AuctionDbContext.cs`
- `src/AuctionService/Services/GrpcAuctionService.cs`
- `src/AuctionService/Consumers/*`

Responsibilities:

- Owns auctions and car item details.
- Persists to PostgreSQL through EF Core.
- Seeds demo auctions in `Data/DbInitializer.cs`.
- Publishes `AuctionCreated`, `AuctionUpdated`, and `AuctionDeleted`.
- Consumes `BidPlaced` to update `CurrentHighBid`.
- Consumes `AuctionFinished` to set winner/sold amount/status.
- Exposes gRPC lookup used by `BiddingService` when its local auction snapshot is missing.
- Uses MassTransit EF outbox to make event publishing more reliable.

### BiddingService

Main files:

- `src/BiddingService/Controllers/BidsController.cs`
- `src/BiddingService/Services/CheckAuctionFinished.cs`
- `src/BiddingService/Services/GrpcAuctionClient.cs`
- `src/BiddingService/Consumers/AuctionCreatedConsumer.cs`
- `src/BiddingService/Models/*`

Responsibilities:

- Accepts and validates bids.
- Stores bids in MongoDB via MongoDB.Entities.
- Keeps a small local auction snapshot from `AuctionCreated`.
- Falls back to AuctionService gRPC if an auction snapshot is missing.
- Publishes `BidPlaced`.
- Background worker checks expired auctions every 5 seconds and publishes `AuctionFinished`.

Bid statuses:

- `Accepted`
- `AcceptedBelowReserve`
- `TooLow`
- `Finished`

### SearchService

Main files:

- `src/SearchService/Controllers/SearchController.cs`
- `src/SearchService/Data/DbInitializer.cs`
- `src/SearchService/Services/AuctionSvcHttpClient.cs`
- `src/SearchService/Consumers/*`

Responsibilities:

- Provides paged/searchable auction listings from MongoDB.
- Creates text index over `Make`, `Model`, and `Color`.
- Bootstraps from AuctionService on startup using `GET /api/auctions?date=...`.
- Tracks auction lifecycle events and bid events to keep the read model current.

Search parameters:

- `searchTerm`
- `pageNumber`
- `pageSize`
- `seller`
- `winner`
- `orderBy`: `make`, `new`, default by auction end
- `filterBy`: `live`, `endingSoon`, `finished`

### NotificationService

Main files:

- `src/NotificationService/Program.cs`
- `src/NotificationService/Hubs/NotificationHub.cs`
- `src/NotificationService/Consumers/*`

Responsibilities:

- Consumes `AuctionCreated`, `BidPlaced`, and `AuctionFinished`.
- Broadcasts SignalR messages with the same method names:
  - `AuctionCreated`
  - `BidPlaced`
  - `AuctionFinished`

### IdentityService

Main files:

- `src/IdentityService/HostingExtensions.cs`
- `src/IdentityService/Config.cs`
- `src/IdentityService/SeedData.cs`
- `src/IdentityService/Services/CustomProfiileService.cs`

Responsibilities:

- Hosts Duende IdentityServer.
- Stores users in PostgreSQL through ASP.NET Identity.
- Seeds users `alice` and `bob` with password `Pass123$`.
- Adds a custom `username` claim used by backend authorization checks and frontend session state.
- Defines frontend client `nextApp`.

Note: most Razor files under `IdentityService/Pages` are standard IdentityServer UI plumbing.

## Shared Events

Contracts live in `src/Contracts`:

- `AuctionCreated`
- `AuctionUpdated`
- `AuctionDeleted`
- `BidPlaced`
- `AuctionFinished`

Event flow summary:

```text
AuctionService create/update/delete
  -> RabbitMQ contracts
  -> SearchService projection updates
  -> NotificationService SignalR broadcasts

BiddingService place bid
  -> BidPlaced
  -> AuctionService current high bid update
  -> SearchService current high bid update
  -> NotificationService live bid broadcast

BiddingService auction finish worker
  -> AuctionFinished
  -> AuctionService final status
  -> SearchService final status
  -> NotificationService finished-auction broadcast
```

## Frontend

Main files:

- `frontend/web-app/auth.ts`
- `frontend/web-app/lib/fetchWrapper.ts`
- `frontend/web-app/app/actions/auctionActions.ts`
- `frontend/web-app/app/providers/SignalRProvider.tsx`
- `frontend/web-app/hooks/useAuctionStore.ts`
- `frontend/web-app/hooks/useBidStore.ts`
- `frontend/web-app/hooks/useParamsStore.ts`
- `frontend/web-app/app/auctions/Listings.tsx`
- `frontend/web-app/app/auctions/details/[id]/page.tsx`
- `frontend/web-app/app/auctions/AuctionForm.tsx`

Frontend behavior:

- NextAuth uses Duende provider `id-server` with issuer `http://localhost:5001`.
- `fetchWrapper.ts` sends requests to `http://localhost:6001/`.
- Server actions in `app/actions/auctionActions.ts` are the main API boundary.
- Zustand stores listing data, search params, and bid state.
- `SignalRProvider.tsx` connects to `http://localhost:6001/notifications`.
- Live bids update list cards and detail bid lists.
- Create/update/delete forms call server actions, then navigate back to auction detail or home.

PowerShell note: paths like `app/auctions/details/[id]/page.tsx` require `Get-Content -LiteralPath` because brackets are wildcard syntax.

## Common Commands

From repo root:

```powershell
dotnet build Carsties.sln --no-restore
```

Frontend:

```powershell
cd frontend\web-app
npm.cmd run lint
npm.cmd run dev
```

Use `npm.cmd` in PowerShell if `npm.ps1` is blocked by execution policy.

Docker stack:

```powershell
docker compose up --build
```

## Verification State

Known good checks from this workspace:

- `dotnet build Carsties.sln --no-restore` passes.
- `npm.cmd run lint` passes.

Current warning:

- `AutoMapper 14.0.0` reports a high-severity vulnerability advisory in AuctionService, BiddingService, and SearchService.

## Local Development Notes

- Most backend services target .NET 9.
- `IdentityService` targets .NET 8.
- Auction writes are authorized and compare `auction.Seller` to `User.Identity?.Name`.
- The JWT `NameClaimType` is set to `username` in protected backend services.
- AuctionService calls `UseAuthentication()` and `UseAuthorization()`.
- Gateway uses YARP authorization policies for protected write routes.
- SearchService has retry policy for AuctionService bootstrap reads.
- AuctionService publishes events before `SaveChangesAsync()`, relying on MassTransit EF outbox configuration.
- BiddingService local auction documents are created from `AuctionCreated`; if an event was missed, gRPC lookup can still allow bidding.
- SearchService is eventually consistent, so list/detail values can briefly differ after writes.

## Safe Change Strategy

When modifying this repo:

1. Identify whether the change belongs to the authoritative model, a projection, or the frontend only.
2. If changing auction shape, update all relevant DTOs, entities/models, AutoMapper profiles, contracts, frontend types, and projections.
3. If changing event payloads, update every consumer in `AuctionService`, `SearchService`, `BiddingService`, and `NotificationService`.
4. If changing public routes, update `GatewayService` config and frontend `auctionActions.ts`/`fetchWrapper.ts` callers.
5. Re-run `dotnet build Carsties.sln --no-restore` and `npm.cmd run lint`.

