# MineSafe-Backend

The backend of the Mine Safety Reporting System is responsible for managing the system's data, business logic, security, reporting workflow, synchronization, and communication with the frontend applications.

Development is divided between two backend developers:

- **Michael ΓÇô Database / SQL**
  - Create and maintain the Microsoft SQL Server database.
  - Build the tables defined in the agreed ERD.
  - Configure primary keys, foreign keys, relationships, constraints, and indexes.
  - Create seed data and SQL queries.
  - Manage database schema changes throughout development.

- **Rikus ΓÇô REST API / ASP.NET Core**
  - Build the ASP.NET Core REST API.
  - Connect the API to the SQL Server database.
  - Develop authentication and authorization.
  - Create endpoints for reports, locations, accounts, reviews, notifications, attachments, and administration.
  - Handle report synchronization between the mobile application and the server.

Both developers work within the same GitHub repository so that database and API changes remain synchronized.

The backend follows the agreed Entity Relationship Diagram (ERD) as the main reference for table names, field names, relationships, and data structure.

The main backend technologies are:

- **Microsoft SQL Server** ΓÇô Database
- **SQL** ΓÇô Database creation and queries
- **ASP.NET Core Web API** ΓÇô REST API
- **C#** ΓÇô API development
- **GitHub** ΓÇô Version control and collaboration

The backend will support key system functionality including:

- User authentication and role-based access
- Mine location management
- Safety report submission
- Offline report synchronization
- Evidence attachment handling
- Supervisor report review
- Report escalation
- Audit history
- Notifications
- Safety officer report management
- Administrator account and location management

The goal is to keep the database and REST API clearly separated while ensuring that both use the exact same agreed data structure throughout development.
