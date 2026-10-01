# Sons

Os três sons foram feitos no SFX Forge.

| Arquivo | Quando toca |
|---|---|
| `pronto.wav` | o Claude terminou |
| `atencao.wav` | o Claude precisa de você (permissão) |
| `falha.wav` | a resposta falhou (`StopFailure`) |

Para trocar um som, substitua o arquivo por outro WAV com o mesmo nome. Os WAVs vão embutidos no
`.exe` pelo `src/build.ps1`, e uma cópia fica em `site/sons/` para o site tocar.
