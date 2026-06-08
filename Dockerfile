# Use the .NET 10.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["ElsaServer/ElsaServer.csproj", "ElsaServer/"]
COPY ["ElsaStudio/ElsaStudio.csproj", "ElsaStudio/"]
RUN dotnet restore "ElsaServer/ElsaServer.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/ElsaServer"

# Build and publish
RUN dotnet publish "ElsaServer.csproj" -c Release -o /app/publish

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080 (the default for ASP.NET 8/10 non-root containers)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ElsaServer.dll"]
