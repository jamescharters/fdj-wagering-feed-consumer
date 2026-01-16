# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies (layer caching optimisation)
COPY WageringFeedConsumer.csproj .
RUN dotnet restore WageringFeedConsumer.csproj

# Copy source and publish (exclude test project)
COPY Controllers/ Controllers/
COPY Models/ Models/
COPY Properties/ Properties/
COPY Repositories/ Repositories/
COPY Services/ Services/
COPY Program.cs .
COPY appsettings*.json .
RUN dotnet publish WageringFeedConsumer.csproj -c Release -o /app --no-restore

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

ENTRYPOINT ["dotnet", "WageringFeedConsumer.dll"]
