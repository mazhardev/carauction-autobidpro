# Plan: Users Can Add Auctions

## Goal

Authenticated users should be able to create a new car auction from the frontend. The auction must be saved by `AuctionService`, published as an event, indexed by `SearchService`, made available for bidding by `BiddingService`, and announced live through `NotificationService`.

This feature is mostly implemented already. Use this plan to understand, verify, or safely extend the flow.

## User Journey

1. User logs in through IdentityService.
2. User opens the frontend at `http://localhost:3000`.
3. User chooses `Sell my car` from the user menu.
4. User fills out the auction form:
   - make
   - model
   - color
   - year
   - mileage
   - image URL
   - reserve price
   - auction end date/time
5. Frontend sends a create request through the gateway.
6. AuctionService creates the auction with the logged-in user as seller.
7. AuctionService publishes `AuctionCreated`.
8. SearchService stores the auction in its MongoDB read model.
9. BiddingService stores a small auction snapshot so bids can be accepted.
10. NotificationService broadcasts `AuctionCreated` over SignalR.
11. Other connected users see a live toast for the new auction.
12. Creator is redirected to the auction detail page.

## Current Route And Service Flow

```text
Frontend create form
  -> app/actions/auctionActions.ts createAuction()
  -> fetchWrapper.post("auctions", data)
  -> GatewayService POST /auctions
  -> AuctionService POST /api/auctions
  -> PostgreSQL save
  -> publish AuctionCreated
  -> SearchService AuctionCreatedConsumer
  -> BiddingService AuctionCreatedConsumer
  -> NotificationService AuctionCreatedConsumer
  -> SignalR AuctionCreated
```

## Frontend Files

Main files:

- `frontend/web-app/app/nav/UserActions.tsx`
- `frontend/web-app/app/auctions/create/page.tsx`
- `frontend/web-app/app/auctions/AuctionForm.tsx`
- `frontend/web-app/app/actions/auctionActions.ts`
- `frontend/web-app/lib/fetchWrapper.ts`
- `frontend/web-app/app/providers/SignalRProvider.tsx`
- `frontend/web-app/app/components/AuctionCreatedToast.tsx`

Expected behavior:

- `UserActions.tsx` shows `Sell my car` only when a user is logged in.
- `create/page.tsx` renders `AuctionForm`.
- `AuctionForm.tsx` collects all required create fields.
- `createAuction()` calls `POST /auctions`.
- `fetchWrapper.ts` attaches `Authorization: Bearer <accessToken>` when a session exists.
- On success, the user is routed to `/auctions/details/{id}`.
- On error, the form shows a toast.

Frontend validation checklist:

- Required fields are enforced.
- Numeric fields are sent as numbers or accepted by model binding.
- `auctionEnd` is a valid future date.
- Form submit button is disabled until the form is valid and dirty.
- Unauthenticated users should be sent to login before creating an auction.
- Seller should not need to type their username; backend derives it from the token.

## Gateway Files

Main files:

- `src/GatewayService/appsettings.json`
- `src/GatewayService/appsettings.Development.json`
- `src/GatewayService/appsettings.Docker.json`
- `src/GatewayService/Program.cs`

Expected behavior:

- `POST /auctions` maps to `AuctionService /api/auctions`.
- Write routes have `AuthorizationPolicy: default`.
- JWT bearer auth uses IdentityService.
- CORS allows the frontend origin.

Gateway route:

```json
"auctionsWrite": {
  "ClusterId": "auctions",
  "AuthorizationPolicy": "default",
  "Match": {
    "Path": "/auctions/{**catch-all}",
    "Methods": [ "POST", "PUT", "DELETE" ]
  },
  "Transforms": [
    {
      "PathPattern": "api/auctions/{**catch-all}"
    }
  ]
}
```

## AuctionService Files

Main files:

- `src/AuctionService/Controllers/AuctionsController.cs`
- `src/AuctionService/DTOs/CreateAuctionDto.cs`
- `src/AuctionService/DTOs/AuctionDto.cs`
- `src/AuctionService/Entities/Auction.cs`
- `src/AuctionService/Entities/Item.cs`
- `src/AuctionService/RequestHelpers/MappingProfiles.cs`
- `src/AuctionService/Program.cs`

Expected behavior:

- `CreateAuction` requires `[Authorize]`.
- Seller is set from `User.Identity?.Name`.
- Request body maps from `CreateAuctionDto` to `Auction`.
- `Auction.Item` is created from the same DTO.
- `AuctionCreated` is published.
- EF Core saves the auction to PostgreSQL.
- Response returns `201 Created` with the new `AuctionDto`.

Important implementation detail:

```text
User.Identity.Name comes from the JWT username claim.
Program.cs sets TokenValidationParameters.NameClaimType = "username".
IdentityService CustomProfileService emits that username claim.
```

Backend validation checklist:

- `Make`, `Model`, `Color`, `ImageUrl`, `ReservePrice`, and `AuctionEnd` are required.
- `AuctionEnd` should ideally be validated as a future date.
- `ReservePrice` should ideally be non-negative.
- `Year` and `Mileage` should ideally have sensible minimum values.
- User must be authenticated.
- Seller should always be taken from the token, never trusted from the request body.

## Event Contract

Main file:

- `src/Contracts/AuctionCreated.cs`

The event should include enough data for:

- Search listing display.
- Bidding auction snapshot.
- Notification toast display.

Current important fields:

- `Id`
- `ReservePrice`
- `Seller`
- `AuctionEnd`
- `Status`
- `Make`
- `Model`
- `Year`
- `Color`
- `Mileage`
- `ImageUrl`

If the create payload changes, update:

- `AuctionService` DTO/entity/mapping.
- `Contracts/AuctionCreated.cs`.
- `SearchService` model/mapping/consumer.
- `BiddingService` consumer/model if bid validation needs the new field.
- `NotificationService` consumer only if broadcast shape changes.
- Frontend `Auction` type and form fields.

## SearchService Projection

Main files:

- `src/SearchService/Consumers/AuctionCreatedConsumer.cs`
- `src/SearchService/Models/Item.cs`
- `src/SearchService/RequestHelpers/MappingProfiles.cs`

Expected behavior:

- Consumes `AuctionCreated`.
- Maps the event into `SearchService.Models.Item`.
- Saves the item to MongoDB.
- New auction appears in `/search` results.

Validation checklist:

- New auction appears on the homepage after creation.
- Filters and search can find it by make/model/color.
- Current high bid starts empty or zero as expected.
- Listing order respects selected filter/order state.

## BiddingService Snapshot

Main files:

- `src/BiddingService/Consumers/AuctionCreatedConsumer.cs`
- `src/BiddingService/Models/Auction.cs`
- `src/BiddingService/Controllers/BidsController.cs`

Expected behavior:

- Consumes `AuctionCreated`.
- Saves a minimal auction document:
  - `ID`
  - `Seller`
  - `AuctionEnd`
  - `ReservePrice`
- Bids can be placed against the new auction.
- Seller cannot bid on their own auction.

Validation checklist:

- A different logged-in user can bid on the new auction.
- Seller receives a clear error if they bid on their own auction.
- Bids below reserve become `AcceptedBelowReserve` when they are highest.
- Bids above reserve become `Accepted`.
- Too-low bids become `TooLow`.

## NotificationService Live Update

Main files:

- `src/NotificationService/Consumers/AuctionCreatedConsumer.cs`
- `src/NotificationService/Hubs/NotificationHub.cs`
- `frontend/web-app/app/providers/SignalRProvider.tsx`
- `frontend/web-app/app/components/AuctionCreatedToast.tsx`

Expected behavior:

- Consumes `AuctionCreated`.
- Broadcasts SignalR method `AuctionCreated`.
- Frontend receives the event.
- Users other than the seller see a new-auction toast.

Validation checklist:

- Open two browsers or sessions.
- Create an auction as user A.
- User B sees a toast.
- User A should not see their own new-auction toast.

## Identity And Authorization

Main files:

- `src/IdentityService/Config.cs`
- `src/IdentityService/SeedData.cs`
- `src/IdentityService/Services/CustomProfiileService.cs`
- `frontend/web-app/auth.ts`

Expected behavior:

- Frontend signs in with Duende client `nextApp`.
- Session includes `accessToken`.
- Session user includes `username`.
- Gateway and AuctionService validate the JWT.
- AuctionService sees `User.Identity.Name` as username.

Seed users:

- `alice` / `Pass123$`
- `bob` / `Pass123$`

## End-To-End Acceptance Criteria

The feature is complete when:

1. Unauthenticated users cannot submit a create auction request.
2. Authenticated users can open `/auctions/create`.
3. Authenticated users can submit a valid auction form.
4. Auction is saved in AuctionService PostgreSQL database.
5. Response redirects user to the new auction detail page.
6. New auction appears in homepage search/listing results.
7. New auction has the logged-in user as seller.
8. Other users receive a live `AuctionCreated` toast.
9. BiddingService receives the auction snapshot.
10. Other users can place bids on the new auction.
11. Seller cannot bid on their own auction.
12. Invalid form data returns clear errors.
13. `dotnet build Carsties.sln --no-restore` passes.
14. `npm.cmd run lint` passes.

## Manual Test Script

1. Start infrastructure and services:

```powershell
docker compose up --build
```

2. Start frontend if it is not included in compose:

```powershell
cd frontend\web-app
npm.cmd run dev
```

3. Login as `alice`.
4. Go to `Sell my car`.
5. Create auction:

```text
Make: Toyota
Model: Supra
Color: Red
Year: 2022
Mileage: 12000
Image URL: https://cdn.pixabay.com/photo/2016/05/06/16/32/car-1376190_960_720.jpg
Reserve price: 50000
Auction end: a future date
```

6. Confirm redirect to details page.
7. Confirm auction appears on homepage.
8. Login as `bob` in another browser.
9. Confirm `bob` can bid.
10. Confirm `alice` cannot bid on her own auction.

## Automated/Build Checks

Run from repo root:

```powershell
dotnet build Carsties.sln --no-restore
```

Run from frontend folder:

```powershell
cd frontend\web-app
npm.cmd run lint
```

## Recommended Hardening

These are useful improvements if the feature needs production polish:

- Add server-side validation for future `AuctionEnd`.
- Add numeric validation for `Year`, `Mileage`, and `ReservePrice`.
- Show create page only to authenticated users or redirect unauthenticated users to login.
- Avoid hardcoded frontend API base URL by moving `http://localhost:6001/` to an environment variable.
- Add integration tests for `POST /api/auctions`.
- Add frontend tests for `AuctionForm` create mode.
- Add idempotency or duplicate-handling in event consumers.
- Review the AutoMapper vulnerability warning and upgrade when safe.

