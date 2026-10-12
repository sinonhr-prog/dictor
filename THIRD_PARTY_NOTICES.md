# Сторонние компоненты

Портативная сборка распространяется с оригинальными уведомлениями в папке `licenses`. Копии находятся также в этом репозитории. Лицензии этих компонентов не определяют лицензию оригинального кода Диктора.

| Компонент | Источник | Уведомление |
|---|---|---|
| Whisper.net и пакеты native runtime 1.9.1 | https://github.com/sandrohanea/whisper.net | [MIT](licenses/Whisper.net.txt) |
| whisper.cpp / ggml | https://github.com/ggml-org/whisper.cpp | [MIT](licenses/whisper.cpp.txt) |
| Модели Whisper large-v3-turbo и small | https://github.com/openai/whisper ; https://huggingface.co/ggerganov/whisper.cpp | [MIT](licenses/Whisper-model.txt) |
| Silero VAD 6.2.0 | https://github.com/snakers4/silero-vad ; https://huggingface.co/ggml-org/whisper-vad | [MIT](licenses/Silero-VAD.txt) |
| NAudio 2.2.1 | https://github.com/naudio/NAudio | [MIT](licenses/NAudio.txt) |
| .NET Runtime | https://github.com/dotnet/runtime | [MIT](licenses/Dotnet.txt), [сторонние уведомления](licenses/Dotnet-third-party.txt) |
| .NET Windows Desktop | https://github.com/dotnet/windowsdesktop | [MIT](licenses/WindowsDesktop.txt) |

Ревизии, ссылки, размеры и SHA-256 моделей указаны в `models/manifest.json` готового комплекта. Зависимости исходников закреплены в `packages.lock.json`. При изменении состава сборки лицензии необходимо проверить заново.
