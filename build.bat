@echo off
chcp 65001 > nul
echo ========================================================
echo   🎮 DESKTOP PET WORLD - BUILD RELEASE SCRIPT (SELF-CONTAINED)
echo ========================================================
echo.

set PROJECT_DIR=%~dp0
cd /d "%PROJECT_DIR%"

set OUTPUT_DIR=%PROJECT_DIR%Build\Release\win-x64
set PUBLISH_DIR=%PROJECT_DIR%Publish\DesktopPetWorld

echo [1/5] Kiểm tra môi trường .NET SDK...
if exist "C:\dotnet\dotnet.exe" (
    set "DOTNET_CMD=C:\dotnet\dotnet.exe"
) else (
    where dotnet >nul 2>nul
    if %errorlevel% neq 0 (
        echo [!] Khong tim thay dotnet CLI trong PATH hoac C:\dotnet.
        echo [!] Vui long cai dat .NET 8 SDK tu https://dot.net
        pause
        exit /b 1
    )
    set "DOTNET_CMD=dotnet"
)

echo Đang sử dụng: %DOTNET_CMD%
%DOTNET_CMD% --version
%DOTNET_CMD% --list-sdks | findstr /R "^[0-9][0-9]*\." >nul
if %errorlevel% neq 0 (
    echo [!] Dotnet da duoc tim thay nhung khong co .NET SDK.
    echo [!] Vui long cai dat .NET 8 SDK tu https://dot.net
    pause
    exit /b 1
)
echo.

echo [2/5] Dọn dẹp bản build cũ...
if exist "%PROJECT_DIR%Build" rd /s /q "%PROJECT_DIR%Build"
if exist "%PROJECT_DIR%Publish" rd /s /q "%PROJECT_DIR%Publish"
if exist "%PROJECT_DIR%bin" rd /s /q "%PROJECT_DIR%bin"
if exist "%PROJECT_DIR%obj" rd /s /q "%PROJECT_DIR%obj"
echo Đã dọn dẹp sạch sẽ.
echo.

echo [3/5] Khôi phục gói thư viện (Restore)...
%DOTNET_CMD% restore DesktopPet.csproj
if %errorlevel% neq 0 (
    echo [!] Lỗi trong quá trình restore!
    pause
    exit /b %errorlevel%
)
echo.

echo [4/5] Biên dịch và đóng gói Self-Contained win-x64...
%DOTNET_CMD% publish DesktopPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT_DIR%"
if %errorlevel% neq 0 (
    echo [!] Lỗi trong quá trình Publish!
    pause
    exit /b %errorlevel%
)
echo.

echo [5/5] Sao chép tài nguyên và chuẩn bị thư mục phân phối...
if not exist "%PUBLISH_DIR%" mkdir "%PUBLISH_DIR%"
xcopy "%OUTPUT_DIR%\*" "%PUBLISH_DIR%\" /E /Y /I > nul

if exist "%PROJECT_DIR%Data" (
    xcopy "%PROJECT_DIR%Data\*" "%PUBLISH_DIR%\Data\" /E /Y /I > nul
)
if exist "%PROJECT_DIR%Assets" (
    xcopy "%PROJECT_DIR%Assets\*" "%PUBLISH_DIR%\Assets\" /E /Y /I > nul
)

echo.
echo ========================================================
echo   ✅ BUILD THÀNH CÔNG RỰC RỠ!
echo ========================================================
echo Bản build độc lập (Self-contained) nằm tại:
echo %OUTPUT_DIR%\DesktopPet.exe
echo.
echo Thư mục phân phối (Portable) sẵn sàng chia sẻ:
echo %PUBLISH_DIR%
echo ========================================================
echo.
pause
