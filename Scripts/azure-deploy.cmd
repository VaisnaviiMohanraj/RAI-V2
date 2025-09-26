@echo off
echo Azure App Service Deployment Script
echo ===================================

:: Build Frontend
echo Building React Frontend...
cd Frontend
echo Installing npm packages...
call npm install --no-optional --legacy-peer-deps
if %ERRORLEVEL% neq 0 (
    echo Trying npm install without optional dependencies...
    call npm install --ignore-scripts --no-optional
    if %ERRORLEVEL% neq 0 goto error
)
echo Building frontend...
call npm run build
if %ERRORLEVEL% neq 0 goto error
cd ..

:: Build Backend
echo Building .NET Backend...
cd Backend
call dotnet restore
if %ERRORLEVEL% neq 0 goto error
call dotnet publish --configuration Release --output %DEPLOYMENT_TARGET%
if %ERRORLEVEL% neq 0 goto error
cd ..

:: Copy Frontend to wwwroot
echo Copying Frontend to wwwroot...
if exist "Frontend\dist" (
    xcopy /E /I /Y "Frontend\dist\*" "%DEPLOYMENT_TARGET%\wwwroot\"
    echo Frontend copied successfully
) else (
    echo Warning: Frontend build not found
)

:: Copy web.config to wwwroot for SPA routing
if exist "Frontend\public\web.config" (
    copy "Frontend\public\web.config" "%DEPLOYMENT_TARGET%\wwwroot\"
    echo web.config copied for SPA routing
)

echo Deployment completed successfully!
goto end

:error
echo Deployment failed with error code %ERRORLEVEL%
exit /b %ERRORLEVEL%

:end
echo Done.
