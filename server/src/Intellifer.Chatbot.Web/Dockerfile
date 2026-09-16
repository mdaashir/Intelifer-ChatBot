FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore against the project files alone so this layer is only invalidated when a
# dependency actually changes, not on every source edit.
COPY server/src/Intellifer.Chatbot.Domain/*.csproj server/src/Intellifer.Chatbot.Domain/
COPY server/src/Intellifer.Chatbot.Application/*.csproj server/src/Intellifer.Chatbot.Application/
COPY server/src/Intellifer.Chatbot.Infrastructure/*.csproj server/src/Intellifer.Chatbot.Infrastructure/
COPY server/src/Intellifer.Chatbot.Web/*.csproj server/src/Intellifer.Chatbot.Web/
RUN dotnet restore server/src/Intellifer.Chatbot.Web/Intellifer.Chatbot.Web.csproj

COPY server/src/ server/src/
RUN dotnet publish server/src/Intellifer.Chatbot.Web/Intellifer.Chatbot.Web.csproj \
    --configuration Release \
    --output /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Intellifer.Chatbot.Web.dll"]
