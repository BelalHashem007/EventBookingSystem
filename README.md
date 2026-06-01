# Evently - Event Booking System

Evently is an ASP.NET Core MVC web application for browsing events, booking tickets, and managing event reservations. Users can create bookings, pay online, receive notifications, and view their booking history, while admins can manage events, users, bookings, and dashboard statistics.

## Features

### User Features

- Browse paginated available events from the home page.
- View event details and available ticket types.
- Register, log in, and log out using ASP.NET Core Identity.
- Create ticket bookings with pending reservation expiry.
- View paginated booking history and booking details.
- Cancel pending bookings.
- Pay for bookings through Stripe Checkout.
- Receive booking notifications through SignalR.
- Receive booking-related emails when email settings are configured.

### Admin Features

- Admin dashboard with event, user, booking, and revenue statistics.
- Create, edit, view, and cancel events.
- Manage event ticket types.
- View all bookings.
- View registered users.
- Role-based admin access through ASP.NET Core Identity.

## System Flow

The diagram below shows the main paths through the application: browsing and booking, Stripe payment confirmation, domain-event notifications, background expiry, and admin management.

```mermaid
flowchart TD
    subgraph userFlow [User Flow]
        BrowseEvents[Browse Events] --> EventDetails[View Event Details]
        EventDetails --> Authenticated{Logged in?}
        Authenticated -->|No| AuthModal[Login / Register Modal]
        AuthModal --> CreateBooking[Create Booking]
        Authenticated -->|Yes| CreateBooking
        CreateBooking --> PendingBooking[Pending Booking]
        PendingBooking --> CancelBooking[Cancel Booking]
    end

    subgraph paymentFlow [Payment Flow]
        PendingBooking --> StripeCheckout[Stripe Checkout]
        StripeCheckout --> StripeWebhook[Stripe Webhook]
        StripeWebhook --> ConfirmedBooking[Booking Confirmed]
    end

    subgraph domainEvents [Domain Events]
        PendingBooking --> BookingCreatedEvent[Booking Created Event]
        BookingCreatedEvent --> CreatedEmail[Send Email]
        BookingCreatedEvent --> CreatedNotification[SignalR Notification]
        ConfirmedBooking --> BookingConfirmedEvent[Booking Confirmed Event]
        BookingConfirmedEvent --> ConfirmedEmail[Send Email]
        BookingConfirmedEvent --> ConfirmedNotification[SignalR Notification]
    end

    subgraph backgroundJobs [Background Jobs]
        PendingBooking --> ExpiryJob[Booking Expiry Job]
        ExpiryJob -->|Unpaid past expiry| ExpiredBooking[Booking Expired]
    end

    subgraph adminFlow [Admin Flow]
        AdminLogin[Admin Login] --> AdminDashboard[Admin Dashboard]
        AdminDashboard --> ManageEvents[Manage Events and Ticket Types]
        AdminDashboard --> ViewBookings[View All Bookings]
        AdminDashboard --> ViewUsers[View Users]
    end
```

## Technologies Used

- .NET 10
- ASP.NET Core MVC and Razor Views
- Entity Framework Core 10
- SQL Server
- ASP.NET Core Identity
- SignalR
- Stripe.net
- MailKit and MimeKit
- Bootstrap
- jQuery and jQuery Validation
- Docker and Docker Compose
- Bogus (development data seeding)

## Project Structure

```text
EventBookingSystem/
  Areas/Admin/           Admin controllers and views
  Api/Controllers/       API endpoints for auth, notifications, email, and Stripe webhooks
  Controllers/           MVC controllers for public/user flows
  Data/                  EF Core DbContext, migrations, and identity seeding
  DomainEvents/          Booking email and notification event handlers
  Extensions/            Startup helpers (database migration and dev seeding)
  Hubs/                  SignalR notification hub
  Models/                Domain models
  Repositories/          Repository and Unit of Work implementation
  Services/              Application services
  Views/                 Razor views
  wwwroot/               Static assets
```

## Prerequisites

- .NET 10 SDK
- SQL Server or SQL Server Express (for local setup without Docker)
- Docker Desktop (optional, for containerized setup)
- Stripe account for payment testing
- SMTP email account if you want email notifications

## Setup

### Docker Setup

Run the app and SQL Server together with Docker Compose from the repository root.

**Requirements:** Docker Desktop, plus Stripe and mail settings exported as environment variables (see below).

Create a `.env` file in the repository root (or export these variables in your shell):

```env
STRIPE_SECRET_KEY=sk_test_your_secret_key
STRIPE_PUBLISHABLE_KEY=pk_test_your_publishable_key
STRIPE_WEBHOOK_SECRET=whsec_your_webhook_secret
MAIL_SETTINGS_EMAIL=your-email@example.com
MAIL_SETTINGS_DISPLAY_NAME=Evently
MAIL_SETTINGS_PASSWORD=your-email-password
MAIL_SETTINGS_HOST=smtp.example.com
MAIL_SETTINGS_PORT=587
```

Start the stack:

```powershell
docker compose up --build
```

The web app listens on `http://localhost:8080`. SQL Server runs in a separate container; migrations and dev event seeding run automatically when the app starts.

`ApplicationSettings__BaseUrl` and a default admin account (`admin@example.com` / `AdminPassword123`) are preconfigured in `docker-compose.yml`. Override them in the `webapp` service `environment` section if needed.

Notes:

- The compose file wires `ConnectionStrings__DefaultConnection` to the `sqlserver` service — no manual migration step is required.
- For Stripe webhooks against the container, forward events to `http://localhost:8080/api/webhook` instead of the HTTPS local dev URL.

### Local Setup

From the repository root, move into the ASP.NET Core project folder:

```powershell
cd .\EventBookingSystem
```

Restore NuGet packages and local .NET tools:

```powershell
dotnet restore
dotnet tool restore
```

Configure local secrets. Replace the values with your local SQL Server, Stripe, email, and admin credentials:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=EventBookingSystem;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
dotnet user-secrets set "AdminSeed:Password" "AdminPassword123"

dotnet user-secrets set "Stripe:SecretKey" "sk_test_your_secret_key"
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_your_publishable_key"
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_your_webhook_secret"

dotnet user-secrets set "MailSettings:Email" "your-email@example.com"
dotnet user-secrets set "MailSettings:DisplayName" "Evently"
dotnet user-secrets set "MailSettings:Password" "your-email-password"
dotnet user-secrets set "MailSettings:Host" "smtp.example.com"
dotnet user-secrets set "MailSettings:Port" "587"
```

Notes:

- `ConnectionStrings:DefaultConnection` is required.
- `ApplicationSettings:BaseUrl` is used in booking emails (it's in appSettings for local dev). For production, set it to the deployed site URL.
- `AdminSeed:Email` and `AdminSeed:Password` are optional, but if one is configured, both must be configured.
- Stripe settings are required for checkout and webhook payment confirmation.
- Mail settings are required for booking email notifications.

Apply EF Core migrations (optional — migrations also run automatically on startup):

```powershell
dotnet ef database update
```

On first startup, if the database is empty, the app applies pending migrations and seeds 50 sample events with ticket types for local development. Admin seeding still runs separately when `AdminSeed:Email` and `AdminSeed:Password` are configured.

Run the application:

```powershell
dotnet run
```

Open one of the launch URLs:

- `https://localhost:7235`
- `http://localhost:5212`

## Stripe Webhooks

For local webhook testing, install the [Stripe CLI](https://docs.stripe.com/stripe-cli/install) and forward events to the app:

```powershell
stripe listen --forward-to https://localhost:7235/api/webhook
```

Use the webhook signing secret from the Stripe CLI output as `Stripe:WebhookSecret`.

## Production Configuration

For deployment (including Docker), configure these values as environment variables. When using Docker Compose locally, Stripe and mail settings are read from the host environment via `.env`.

- `ConnectionStrings__DefaultConnection`
- `ApplicationSettings__BaseUrl`
- `AdminSeed__Email`
- `AdminSeed__Password`
- `Stripe__SecretKey`
- `Stripe__PublishableKey`
- `Stripe__WebhookSecret`
- `MailSettings__Email`
- `MailSettings__DisplayName`
- `MailSettings__Password`
- `MailSettings__Host`
- `MailSettings__Port`

## Admin Access

If `AdminSeed:Email` and `AdminSeed:Password` are configured before startup, the app will create or update that account as an admin user after migrations have been applied. Use those credentials to access the admin area.

When running via Docker Compose, the default admin account is `admin@example.com` with password `AdminPassword123` (configured in `docker-compose.yml`).
