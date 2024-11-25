# ChatterBox

A real-time chat application built with ASP.NET Core 8.0 and SignalR, featuring secure user authentication and instant messaging capabilities.

## Prerequisites

Before you begin, ensure you have the following installed:

### Required Software
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community, Professional, or Enterprise)(Key Available)
  - Required workloads:
    - ASP.NET and web development
    - .NET desktop development
- [GitHub Desktop](https://desktop.github.com/)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Visual Studio 2022 includes SQL Server Express LocalDB, which is required for this project.

## Development Setup

### 1. Repository Setup
1. Open GitHub Desktop
2. Click `File` → `Clone Repository`
3. Select the `URL` tab
4. Enter the repository URL: `[your-repository-url]`
5. Choose your local path
6. Click `Clone`

### 2. Visual Studio Configuration
1. Using GitHub Desktop, click `Open in Visual Studio`
2. Once Visual Studio opens:
   - Wait for package restoration to complete
   - Ensure the solution loads completely

### 3. Database Setup
1. Open `Package Manager Console` in Visual Studio:
   - `Tools` → `NuGet Package Manager` → `Package Manager Console`
2. Execute the following command:
   ```powershell
   Update-Database
   ```

### 4. Client Library Installation
1. In Solution Explorer, right-click the project
2. Select `Add` → `Client-Side Library`
3. Configure the following:
   ```
   Provider: unpkg
   Library: @microsoft/signalr@latest
   Target Location: wwwroot/lib/microsoft/signalr
   ```
4. Click `Install`

### 5. Application Launch
1. Set `ChatterBox` as the startup project
2. Press `F5` or click `IIS Express` to run the application
3. The application will launch in your default browser

## Project Structure

```
ChatterBox/
├── Controllers/    # Request handlers
├── Models/         # Data models
├── Views/          # UI templates
├── Hubs/          # SignalR real-time communication
├── Data/          # Database context
└── wwwroot/       # Static files
```

## Troubleshooting

### Database Initialization Issues
```powershell
# In Package Manager Console
Drop-Database
Update-Database
```

### Port Conflicts
1. Close Visual Studio
2. End IIS-related processes in Task Manager
3. Restart Visual Studio

### Package Restore Failed
1. Clear NuGet cache:
   ```powershell
   dotnet nuget locals all --clear
   ```
2. Restore packages:
   ```powershell
   dotnet restore
   ```

   #Run as Admin if all else is done and still not working