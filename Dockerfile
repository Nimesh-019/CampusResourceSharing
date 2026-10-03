# Build stage using official .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (optimizes Docker layer caching)
COPY ["CampusResourceSharing/CampusResourceSharing.csproj", "CampusResourceSharing/"]
RUN dotnet restore "CampusResourceSharing/CampusResourceSharing.csproj"

# Copy all source files and publish release build
COPY . .
WORKDIR "/src/CampusResourceSharing"
RUN dotnet publish "CampusResourceSharing.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage using official .NET 10 ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Default port configuration (Render assigns dynamic PORT at runtime)
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CampusResourceSharing.dll"]
