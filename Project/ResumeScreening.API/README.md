# AI-Powered Resume Screening System
### MCA Major Project — Step 1: Foundation Setup

---

## Folder Structure (Current)

```
ResumeScreening.API/
├── Controllers/
│   └── AuthController.cs        ← Login & Register endpoints
├── Data/
│   └── AppDbContext.cs          ← EF Core DbContext + all tables
├── DTOs/
│   └── AuthDtos.cs              ← Request/Response data shapes
├── Helpers/
│   └── JwtHelper.cs             ← JWT token generation
├── Models/
│   ├── User.cs
│   ├── Job.cs
│   ├── Resume.cs
│   ├── ScoreResult.cs
│   └── Application.cs
├── appsettings.json             ← Connection strings & config
├── Program.cs                   ← App startup & middleware
└── ResumeScreening.API.csproj   ← NuGet packages
```

---

## Setup Instructions

### Step 1 — Restore NuGet packages
```bash
cd "A:\MCA Project\Project\ResumeScreening.API"
dotnet restore
```

### Step 2 — Update appsettings.json
Open `appsettings.json` and update:
- `ConnectionStrings.DefaultConnection` → your SQL Server instance
- `JwtSettings.SecretKey` → any random 32+ character string
- `AzureBlobStorage.ConnectionString` → from Azure Portal (do this in Week 2)

### Step 3 — Create the first migration
```bash
dotnet ef migrations add InitialCreate
```
This generates a `Migrations/` folder with the full database schema.

### Step 4 — Apply migration (create the database)
```bash
dotnet ef database update
```
Open SQL Server Management Studio — you should see `ResumeScreeningDb` with all 5 tables.

### Step 5 — Run the API
```bash
dotnet run
```
Open browser → `https://localhost:5001`
Swagger UI loads automatically. You can test Register and Login right there.

---

## Test the Auth Flow

### Register a new HR Admin
```
POST https://localhost:5001/api/auth/register
Content-Type: application/json

{
  "fullName": "Your Name",
  "email": "hr@test.com",
  "password": "Test@123",
  "role": "HRAdmin"
}
```

### Login
```
POST https://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "hr@test.com",
  "password": "Test@123"
}
```
Copy the `token` from the response — you'll use it as `Bearer {token}` in all future requests.

---

## What's Coming Next

| Step | What to build |
|------|--------------|
| Step 2 | Job CRUD API + Azure Blob setup |
| Step 3 | Resume upload + PdfPig text extraction |
| Step 4 | ML.NET scoring engine |
| Step 5 | Dashboard, filters, export |
| Step 6 | Angular frontend |
| Step 7 | Azure deployment |
