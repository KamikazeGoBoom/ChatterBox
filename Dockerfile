FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# Install tools and packages
RUN dotnet new tool-manifest
RUN dotnet tool install --local dotnet-ef
RUN dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# Build and publish
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Create data directory
RUN mkdir -p /app/data

EXPOSE 8080
ENTRYPOINT ["dotnet", "ChatterBox.dll"]
