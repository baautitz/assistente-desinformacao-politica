FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AssistenteDesinformacao.TelegramBot -c Release -o /app/telegram-bot \
 && dotnet publish src/AssistenteDesinformacao.Coletor -c Release -o /app/coletor

# Uma imagem só, com todos os processos. O padrão é o bot do Telegram; os outros serviços trocam o entrypoint no compose.
FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY --from=build /app /app
WORKDIR /app/telegram-bot
ENTRYPOINT ["dotnet", "AssistenteDesinformacao.TelegramBot.dll"]
