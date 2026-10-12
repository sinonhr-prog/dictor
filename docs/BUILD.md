# Сборка Диктора

Windows x64, PowerShell 7 и .NET SDK **10.0.401**. Исходники находятся в папке `Диктор`. Зависимости и модели загружаются только при подготовке среды; для запуска готового выпуска SDK не нужен.

## Подготовка

Скачайте репозиторий или клонируйте его:

```powershell
git clone https://github.com/sinonhr-prog/dictor.git
cd dictor
```

Скачайте ZIP SDK 10.0.401 для Windows x64 с [официальной страницы .NET](https://dotnet.microsoft.com/download/dotnet/10.0), проверьте опубликованную Microsoft контрольную сумму и распакуйте SDK в `Диктор/.sdk`. В этой папке должен появиться `dotnet.exe`. Скрипты специально используют локальный SDK, а не системную установку.

Команды далее выполняются из корня репозитория:

```powershell
pwsh -NoProfile -File ./Диктор/tools/build.ps1 -Publish
pwsh -NoProfile -File ./Диктор/tools/download-models.ps1
Copy-Item ./licenses ./Диктор/runtime/licenses -Recurse
```

`build.ps1` выполняет locked restore, Release build с ошибкой на предупреждениях, проверку форматирования, тесты и self-contained publish. Загрузка моделей получает ревизии источников, проверяет SHA-256 и записывает `runtime/models/manifest.json`. Дополнительная модель turbo Q5_0 скачивается для экспериментов, но не включается в портативный комплект. Точные ревизии моделей готового выпуска записаны в его manifest; повторный запуск загрузчика позже может выбрать другую ревизию.

Запуск собранного приложения: `Диктор/runtime/Dictor.exe`. Микрофон включается только при явном начале записи или нажатии проверки уровня.

## Упаковка

В новой сессии PowerShell 7 из корня репозитория:

```powershell
. ./Диктор/tools/env.ps1
& $Dotnet publish ./Диктор/src/Dictor/Dictor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./Диктор/.build/portable-publish
if ($LASTEXITCODE) { throw 'PUBLISH_FAILED' }
./Диктор/tools/package.ps1
```

Результат — `Диктор/dist/Dictor-Portable-win-x64.zip` и `.sha256`. Скрипт использует модели и лицензии из `runtime`, проверяет модели, формирует файловый manifest и переименовывает EXE в `Диктор.exe`. Существующий комплект не перезаписывается: для следующего используйте `-Name Dictor-Portable-win-x64-next` либо новый чистый каталог сборки.

Не коммитьте `runtime`, `dist`, `.sdk`, `.packages`, `.build`, аудио или личные `data`. Модели и готовые сборки распространяются через Releases. Для ручных проверок не используйте личную речь без разрешения её владельца.
