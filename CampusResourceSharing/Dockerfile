# Build stage using official Microsoft .NET 10 SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies as distinct layers
COPY ["CampusResourceSharing.csproj", "./"]
RUN dotnet restore "CampusResourceSharing.csproj"

# Copy the rest of the application and publish release build
COPY . .
RUN dotnet publish "CampusResourceSharing.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage using official Microsoft ASP.NET Core 10 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Expose HTTP port
EXPOSE 8080

# Copy build artifacts to runtime container
COPY --from=build /app/publish .

# Run the ASP.NET Core application
ENTRYPOINT ["dotnet", "CampusResourceSharing.dll"]
