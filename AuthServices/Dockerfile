FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# Set working directory in the container
WORKDIR /src

# Copy the .csproj file explicitly from the current directory
COPY AuthServices.csproj ./

# Restore the project’s dependencies
RUN dotnet restore "AuthServices.csproj"

# Copy the rest of the application files to the container
COPY . . 

# Set working directory to where the project file is located
WORKDIR "/src"

# Build the project
RUN dotnet build "AuthServices.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the project
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AuthServices.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthServices.dll"]
