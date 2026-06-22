# ----- GIAI ĐOẠN 1: Build -----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Docker sẽ tìm "Quiz_Web/Quiz_Web.csproj"
# Tương ứng với "D:\LapTrinhWeb\Quiz_Web\Quiz_Web\Quiz_Web.csproj"
COPY ["Quiz_Web/Quiz_Web.csproj", "Quiz_Web/"]
RUN dotnet restore "Quiz_Web/Quiz_Web.csproj"

# Sao chép mọi thứ khác (bao gồm cả thư mục Quiz_Web)
COPY . .
WORKDIR "/src/Quiz_Web"
RUN dotnet publish "Quiz_Web.csproj" -c Release -o /app/publish

# ----- GIAI ĐOẠN 2: Final -----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080


# Install dependencies for SkiaSharp and fontconfig
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libharfbuzz0b \
    fonts-dejavu \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Quiz_Web.dll"]