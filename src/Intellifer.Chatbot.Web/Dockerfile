FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore against the project files alone so this layer is only invalidated when a
# dependency actually changes, not on every source edit.
COPY src/Intellifer.Chatbot.Domain/*.csproj src/Intellifer.Chatbot.Domain/
COPY src/Intellifer.Chatbot.Application/*.csproj src/Intellifer.Chatbot.Application/
COPY src/Intellifer.Chatbot.Infrastructure/*.csproj src/Intellifer.Chatbot.Infrastructure/
COPY src/Intellifer.Chatbot.Web/*.csproj src/Intellifer.Chatbot.Web/
RUN dotnet restore src/Intellifer.Chatbot.Web/Intellifer.Chatbot.Web.csproj

COPY src/ src/
RUN dotnet publish src/Intellifer.Chatbot.Web/Intellifer.Chatbot.Web.csproj \
    --configuration Release \
    --output /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Intellifer.Chatbot.Web.dll"]
