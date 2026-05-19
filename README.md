# 🤖 AI-Powered Resume Screening System

<div align="center">

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-19-DD0031?style=for-the-badge&logo=angular&logoColor=white)
![Azure](https://img.shields.io/badge/Azure-Cloud-0078D4?style=for-the-badge&logo=microsoftazure&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-Azure_SQL-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![Gemini AI](https://img.shields.io/badge/Gemini-AI_Scoring-4285F4?style=for-the-badge&logo=google&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT_Bearer-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white)

**A full-stack cloud SaaS application that automates candidate screening using AI-powered resume scoring.**  
Built as MCA Major Project — Cloud Computing Specialisation | Chandigarh University | 2025–2026

[Features](#-features) · [Architecture](#-architecture) · [Tech Stack](#-tech-stack) · [Getting Started](#-getting-started) · [API Docs](#-api-endpoints) · [Screenshots](#-screenshots) · [Future Scope](#-future-scope)

</div>

---

## 📌 Overview

Manually reviewing hundreds of resumes for a job posting is time-consuming and inconsistent. This system enables HR professionals to:

- Post job openings with a Job Description (JD) file
- Upload candidate resumes in bulk (PDF)
- **Automatically rank candidates** using dual AI screening — **TF-IDF keyword matching** and **Google Gemini AI** context-aware scoring
- Shortlist, review, and export results — all from a clean Angular dashboard

The entire application is **cloud-hosted on Microsoft Azure**, demonstrating real-world SaaS architecture patterns.

---

## ✨ Features

### 👩‍💼 For HR Admins
- ✅ Create and manage job postings with JD file upload
- ✅ Bulk upload up to 20 candidate PDF resumes per job
- ✅ One-click AI screening — candidates ranked 0–100 with colour-coded score badges
- ✅ View matched keywords highlighted in candidate detail view
- ✅ Shortlist, mark under review, or reject candidates with notes
- ✅ Export shortlisted candidates to Excel (.xlsx)
- ✅ Dashboard with summary stats and score distribution chart

### 👁️ For Viewers (Candidates)
- ✅ View assigned job posting
- ✅ View own score, rank, and score breakdown via "My Status" tab
- ✅ Read-only access — no modification permissions

### 🤖 Dual AI Scoring Engine
- **TF-IDF Method** — Keyword frequency matching, fast, runs locally
- **Gemini AI Method** — Google Gemini API for context-aware semantic scoring
- Score breakdown: Keyword Match (0-60) + Experience (0-10) + Skills (0-15) + Education (0-15)
- Color-coded results: 🟢 Green (70+) · 🟡 Amber (40-69) · 🔴 Red (below 40)
- Real-time screening progress with animated UI feedback

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     CLIENT LAYER                            │
│              Angular 19 SPA (Azure Static Web Apps)         │
│    Dashboard · Jobs · Resume Upload · Rankings · Export     │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS / REST API
┌────────────────────────▼────────────────────────────────────┐
│                      API LAYER                              │
│            .NET 9 Web API (Azure App Service)               │
│    Auth · Jobs · Resumes · Scoring · Dashboard · Export     │
│                  JWT Bearer Authentication                  │
└───────┬──────────────────────┬──────────────────────────────┘
        │                      │
┌───────▼───────┐    ┌─────────▼────────┐    ┌───────────────┐
│  Azure Blob   │    │    Azure SQL     │    │  AI Scoring   │
│   Storage     │    │    Database      │    │   Engine      │
│               │    │                  │    │               │
│  PDF Resumes  │    │  Users · Jobs    │    │  TF-IDF +     │
│  JD Files     │    │  Resumes · Scores│    │  Gemini AI    │
└───────────────┘    └──────────────────┘    └───────────────┘
```

### Data Flow — Resume Screening
```
HR uploads PDFs → Azure Blob Storage
                → iText7 extracts text → Azure SQL (ExtractedText)
                → TF-IDF or Gemini AI scores against JD
                → ScoreResults persisted → Ranked list returned to UI
```

---

## 🛠️ Tech Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | Angular 19, TypeScript, SCSS | HR dashboard, upload UI, rankings |
| **Backend** | .NET 9 Web API, C# | REST APIs, business logic, auth |
| **AI Engine** | TF-IDF + Google Gemini API | Dual scoring: keyword matching & context-aware AI |
| **PDF Parsing** | iText7 (NuGet) | Extract plain text from uploaded PDFs |
| **ORM** | EF Core 9 | Code-first migrations, repository pattern |
| **Database** | Azure SQL (SQL Server) | Structured relational data |
| **File Storage** | Azure Blob Storage | Secure PDF file storage |
| **Auth** | JWT Bearer Tokens | Role-based access (HRAdmin / Viewer) |
| **Hosting** | Azure App Service | .NET API deployment |
| **Frontend Hosting** | Azure Static Web Apps | Angular SPA deployment |
| **Export** | EPPlus (NuGet) | Export shortlisted candidates to Excel |
| **Testing** | xUnit + Moq | Unit tests for ScoringService |
| **Docs** | Swagger / OpenAPI | Auto-generated API documentation |
| **Charts** | Chart.js | Score distribution bar chart |

---

## 📁 Project Structure

```
ResumeScreening/
│
├── ResumeScreening.API/                  # .NET 9 Web API
│   ├── Controllers/
│   │   ├── AuthController.cs             # Register, Login
│   │   └── JobsController.cs             # Jobs, Resumes, Screening, Export
│   ├── Services/
│   │   ├── AiScoringService.cs           # Google Gemini AI scoring
│   │   ├── TfIdfScoringService.cs        # TF-IDF keyword scoring
│   │   └── BlobService.cs                # Azure Blob Storage operations
│   ├── Models/                           # EF Core entities
│   │   ├── User.cs
│   │   ├── Job.cs
│   │   ├── Resume.cs
│   │   ├── ScoreResult.cs
│   │   └── Application.cs
│   ├── DTOs/                             # Request/Response DTOs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Helpers/
│   │   ├── JwtHelper.cs
│   │   ├── TfIdfHelper.cs
│   │   └── PdfExtractor.cs
│   └── appsettings.json
│
├── resume-screening-ui/                  # Angular 19 SPA
│   └── src/app/
│       ├── auth/                         # Login, Register + AuthGuard
│       ├── jobs/                         # JobList, JobCreate, JobDetail
│       └── core/                         # AuthService, JobsService, Interceptor
│
├── ResumeScreening.Tests/                # xUnit test project
│   └── ScoringServiceTests.cs
│
└── README.md
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Node.js 18+](https://nodejs.org/) and Angular CLI (`npm install -g @angular/cli`)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/) or Azure SQL instance
- [Azure Storage Account](https://portal.azure.com/) (for Blob Storage)
- [Git](https://git-scm.com/)

---

### 1. Clone the Repository

```bash
git clone https://github.com/Shivi-Chaudhary/ResumeScreening.git
cd ResumeScreening
```

---

### 2. Configure the Backend

Navigate to the API project:

```bash
cd ResumeScreening.API
```

Create a `appsettings.Development.json` file (do **not** commit this — it's in `.gitignore`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=ResumeScreeningDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
  },
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_KEY;",
    "ContainerName": "resumes"
  },
  "JwtSettings": {
    "SecretKey": "YOUR_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "ResumeScreeningAPI",
    "Audience": "ResumeScreeningClient",
    "ExpiryHours": 8
  },
  "GeminiAI": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.0-flash"
  }
}
```

> 💡 **Tip:** Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) locally instead of a config file:
> ```bash
> dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key-here"
> ```

---

### 3. Run Database Migrations

```bash
dotnet ef database update
```

This creates all tables: `Users`, `Jobs`, `Resumes`, `ScoreResults`, `Applications`.

---

### 4. Run the API

```bash
dotnet run
```

API runs at `https://localhost:7001`  
Swagger UI available at: `https://localhost:7001/swagger`

---

### 5. Configure and Run the Frontend

```bash
cd ../resume-screening-ui
npm install
```

Update `src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7001/api'
};
```

Start the Angular dev server:

```bash
ng serve
```

App available at: `http://localhost:4200`

---

### 6. Run Tests

```bash
cd ../ResumeScreening.Tests
dotnet test
```

---

## ☁️ Azure Deployment

### Deploy the API to Azure App Service

```bash
cd ResumeScreening.API
dotnet publish -c Release -o ./publish

# Using Azure CLI
az webapp deployment source config-zip \
  --resource-group <your-rg> \
  --name <your-app-service-name> \
  --src ./publish.zip
```

### Deploy Angular to Azure Static Web Apps

```bash
cd resume-screening-ui
ng build --configuration production

# Deploy via Azure Static Web Apps CLI
swa deploy ./dist/resume-screening-ui \
  --deployment-token <your-token>
```

### Environment Variables on Azure App Service

Set these in **Configuration → Application Settings** in the Azure Portal:

| Key | Value |
|-----|-------|
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string |
| `AzureBlobStorage__ConnectionString` | Blob Storage connection string |
| `AzureBlobStorage__ContainerName` | `resumes` |
| `JwtSettings__SecretKey` | Your JWT secret (32+ chars) |
| `JwtSettings__Issuer` | `ResumeScreeningAPI` |
| `JwtSettings__Audience` | `ResumeScreeningClient` |
| `GeminiAI__ApiKey` | Your Gemini API key |
| `GeminiAI__Model` | `gemini-2.0-flash` |

---

## 📡 API Endpoints

| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| `POST` | `/api/auth/register` | Register new user | Public |
| `POST` | `/api/auth/login` | Login, receive JWT token | Public |
| `GET` | `/api/jobs` | List all active jobs | Auth |
| `POST` | `/api/jobs` | Create job with JD file upload | HRAdmin |
| `DELETE` | `/api/jobs/{id}` | Delete a job | HRAdmin |
| `POST` | `/api/jobs/{id}/resumes` | Bulk upload resumes (up to 20 PDFs) | HRAdmin |
| `GET` | `/api/jobs/{id}/resumes` | List resumes for a job | Auth |
| `POST` | `/api/jobs/{id}/screen` | Trigger AI screening for all resumes | HRAdmin |
| `GET` | `/api/jobs/{id}/rankings` | Get ranked candidates by score | Auth |
| `GET` | `/api/resumes/{id}` | Get resume detail with score breakdown | Auth |
| `PUT` | `/api/resumes/{id}/status` | Update shortlist / reject status | HRAdmin |
| `GET` | `/api/jobs/{id}/export` | Export shortlisted candidates as Excel | HRAdmin |
| `GET` | `/api/dashboard/stats` | Dashboard summary stats | Auth |

Full interactive API documentation available at `/swagger` when running locally.

---

## 🗄️ Database Schema

```
Users           Jobs                Resumes
─────────       ──────────────      ───────────────────
Id              Id                  Id
Email           Title               JobId (FK)
PasswordHash    Description         CandidateName
Role            JdFileUrl           Email
CreatedAt       CreatedBy (FK)      FileUrl
                CreatedAt           ExtractedText
                Status              Status
                                    CreatedAt

ScoreResults                Applications
────────────────────        ──────────────────────
Id                          Id
ResumeId (FK)               ResumeId (FK)
JobId (FK)                  JobId (FK)
Score (0–100)               HRStatus
MatchedKeywords             Notes
ScoredAt                    UpdatedAt
```

---

## 🧪 Testing

The test suite covers the core AI scoring logic:

```bash
dotnet test --verbosity normal
```

**Test cases in `ScoringServiceTests.cs`:**
- Score is 0 for empty resume text
- Score is 100 for perfect keyword match
- Bonus points awarded correctly for experience detection
- Keyword matching is case-insensitive
- Score does not exceed 100 for over-matched resumes
- TF-IDF correctly weights rare keywords over common ones
- Matched keywords list is accurate and deduplicated
- Scoring handles malformed PDF extraction gracefully

---

## 📦 NuGet & NPM Packages

**Backend (NuGet)**

| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` | EF Core with SQL Server |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT authentication middleware |
| `Azure.Storage.Blobs` | Azure Blob Storage SDK |
| `itext7` | PDF text extraction |
| `ClosedXML` | Excel export |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI UI |
| `xunit` + `Moq` | Unit testing + mocking |

**Frontend (NPM)**

| Package | Purpose |
|---------|---------|
| `Angular 19` | Component-based SPA framework |
| `TypeScript` | Typed JavaScript |
| `SCSS` | Styled components |

---

## 🔮 Future Scope

- [ ] **Semantic scoring** — Upgrade from TF-IDF to Azure OpenAI embeddings for deeper contextual matching
- [ ] **Candidate portal** — Self-service portal where candidates apply directly and track status
- [ ] **LinkedIn parsing** — Parse LinkedIn profiles as an alternative to PDF upload
- [ ] **Multi-language support** — Azure Cognitive Services Translator for non-English resumes
- [ ] **Interview scheduling** — Microsoft Graph Calendar API integration
- [ ] **Real-time notifications** — SignalR push notifications when candidates are shortlisted
- [ ] **Containerisation** — Docker + Azure Kubernetes Service (AKS) deployment
- [ ] **Bias detection** — Flag potentially discriminatory scoring patterns using Responsible AI principles

---

## 👩‍💻 Author

**Shivani Chaudhary**  
Cloud Engineer | DevOps Engineer | Storage Specialist  
📧 shivanichaudhary.cv@gmail.com  
🔗 [linkedin.com/in/shiviichaudhary](https://www.linkedin.com/in/shiviichaudhary)  
📍 Bangalore, India

> *MCA in Cloud Computing — Chandigarh University (2024–2026)*  
> *This project was built as my MCA Major Project to demonstrate end-to-end cloud application development on Azure.*

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

⭐ If you found this project useful, please consider giving it a star!

</div>
