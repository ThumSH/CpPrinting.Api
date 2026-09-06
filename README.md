# CP Printing Backend API

Backend API for the CP Printing Management System.

This service powers the desktop application and handles authentication, business logic, database access, role-based permissions, and operational workflows.

## Tech Stack

- C#
- ASP.NET Core
- .NET 9
- Entity Framework Core
- SQL Server
- JWT Authentication
- BCrypt
- REST API

## Core Modules

- Authentication
- User Management
- Customer Management
- Development
- Inventory
- Quality Control
- Delivery Tracking
- Gate Pass
- Audit
- Worker / Operator
- Invoicing
- Reporting
- Activity Logs

## Architecture

Controllers
↓
Services
↓
Entity Framework Core
↓
SQL Server

DTOs and Models are used to separate API contracts from database entities.

## Authentication

The API uses JWT-based authentication.

Passwords are hashed using BCrypt.

Protected endpoints require valid authentication tokens and role-based access where applicable.

## Database

Microsoft SQL Server is used as the main database.

Entity Framework Core is used for:

- data access
- migrations
- relationships
- querying
- persistence

## Frontend

This backend powers the CP Printing desktop application:

https://github.com/ThumSH/cp-printing-system
