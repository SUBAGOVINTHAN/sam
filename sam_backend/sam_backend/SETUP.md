# SAM Machinery ERP — Backend (Visual Studio)

## Project Structure

```
SamErpBackend/
├── Program.cs                  ← Same pattern as RHEOv7
├── Startup.cs                  ← Same pattern as RHEOv7 (with CORS + Session)
├── Helper.cs                   ← Same pattern as RHEOv7
├── appsettings.json            ← DB config under "AppSettings"
├── SamErpBackend.csproj
├── Controllers/
│   └── AuthController.cs       ← All 5 API endpoints
├── Data/
│   └── DB.cs                   ← Same pattern as RHEOv7 (Npgsql instead of SqlClient)
└── Models/
    ├── AppSettingsModel.cs     ← Same pattern as RHEOv7
    └── DTOs.cs                 ← Login/CreateUser request & response models
```

---

## Step 1 — Open in Visual Studio

1. Open **Visual Studio 2022**
2. Click **"Open a project or solution"**
3. Select **`SamErpBackend.csproj`**
4. NuGet packages restore automatically

---

## Step 2 — Configure Database

Open **`appsettings.json`** and set your PostgreSQL credentials:

```json
"AppSettings": {
  "server":   "localhost",
  "db":       "sam_erp",
  "user":     "postgres",
  "password": "YOUR_ACTUAL_PASSWORD",
  "AllowedOrigin": "http://localhost:3000"
}
```

---

## Step 3 — Create the Database

Run **`setup.sql`** in pgAdmin:

1. Open pgAdmin → connect to server
2. Create database named `sam_erp` (if not exists)
3. Open Query Tool → paste `setup.sql` → press **F5**

---

## Step 4 — Run the Backend

Press **F5** in Visual Studio.

- API:     `https://localhost:5098`
- Swagger: `https://localhost:5098/swagger`

---

## Step 5 — Seed Admin User

In Swagger or Postman:

```
POST https://localhost:5098/api/auth/seed
```

Creates: **username:** `admin` / **password:** `Admin@123`

---

## API Endpoints

| Method | Endpoint                | Description                        |
|--------|-------------------------|------------------------------------|
| POST   | `/api/auth/login`       | Login → returns session cookie     |
| POST   | `/api/auth/logout`      | Clear session                      |
| GET    | `/api/auth/me`          | Get current logged-in user         |
| POST   | `/api/auth/seed`        | Create default admin (dev only)    |
| POST   | `/api/auth/create-user` | Create new user (admin only)       |

---

## Key Difference from RHEOv7

| RHEOv7 (SQL Server)           | SAM ERP (PostgreSQL)              |
|-------------------------------|-----------------------------------|
| `System.Data.SqlClient`       | `Npgsql`                          |
| `SqlConnection`               | `NpgsqlConnection`                |
| `SqlCommand`                  | `NpgsqlCommand`                   |
| `SqlDataAdapter`              | `NpgsqlDataAdapter`               |

Everything else — `Program.cs`, `Startup.cs`, `Helper.cs`, `AppSettingsModel`, DB pattern — is identical to your RHEOv7 project.
