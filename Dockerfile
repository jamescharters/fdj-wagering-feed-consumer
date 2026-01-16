# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies (layer caching optimisation)
COPY src/WageringStatsApi.csproj .
RUN dotnet restore WageringStatsApi.csproj

# Copy source and publish (exclude test project)
COPY src/Controllers/ Controllers/
COPY src/Models/ Models/
COPY src/Properties/ Properties/
COPY src/Repositories/ Repositories/
COPY src/Services/ Services/
COPY src/Program.cs .
COPY src/appsettings*.json .
RUN dotnet publish WageringStatsApi.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -D appuser

# Copy published output
COPY --from=build /app .

# Set ownership and switch to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Expose port
EXPOSE 8080

# Health check
#HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
#    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "WageringStatsApi.dll"]
