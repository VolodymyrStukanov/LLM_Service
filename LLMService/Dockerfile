# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build application
COPY . ./
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:PublishReadyToRun=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published app from build stage
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LLMService.dll"]