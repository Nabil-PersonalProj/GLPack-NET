# ===== BUILD STAGE =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["GLPack/GLPack.csproj", "GLPack/"]
RUN dotnet restore "GLPack/GLPack.csproj"

# Install Node for Tailwind build
RUN apt-get update \
    && apt-get install -y nodejs npm \
    && rm -rf /var/lib/apt/lists/*

# Copy the rest of the repo
COPY . .

# Build Tailwind CSS (uses package.json in GLPack/)
WORKDIR /src/GLPack
RUN npm ci
RUN npm run build

# Publish the .NET app
RUN dotnet publish "GLPack.csproj" -c Release -o /app/publish

# ===== RUNTIME STAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Render expects the container to listen on some port; we'll use 10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "GLPack.dll"]
