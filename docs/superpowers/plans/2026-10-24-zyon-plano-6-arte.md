# Zyon — Plano 6: Arte — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trocar a arte provisória (quadrados coloridos e retângulo) por sprites gerados por código: blocos contornados com fronteiras mescladas, paredes, ícones de itens e um jogador em camadas animado (andar, pular, golpe com o item na mão).

**Architecture:** Um programa .NET novo (`src/Zyon.ArtGen`) lê `art/art.json` + os dados do jogo e grava PNGs e um manifesto (`data/client/art/`). O `Zyon.Client` lê o manifesto (`ArtCatalog`), monta as malhas (blocos, paredes e uma malha de mescla) e calcula a pose do jogador (`PlayerAnimator`). O `Zyon.Game` só carrega os PNGs como texturas e desenha. O `EntityView` do Core passa a expor o estado que a animação precisa.

**Tech Stack:** .NET 10, Godot 4.7.2 .NET, xUnit 2.9. Sem bibliotecas de imagem: PNG próprio (`ZLibStream` + CRC32).

**Spec:** `docs/superpowers/specs/2026-10-24-zyon-arte-design.md`.

---

## Convenções

- Tudo do `CLAUDE.md` continua valendo. Core e Client nunca referenciam Godot; o ArtGen também não.
- **Shell:** Bash (Git Bash) em `D:\Projetos\Zyon`. **Commits:** mensagem + `-m` com as duas linhas:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2
  ```
  Nunca `--amend`, nunca `push`.
- **Branch:** `feat/plano-6-arte` (criada pelo controlador).
- **Verificação:** `dotnet test`; nas tarefas do Godot também `dotnet build src/Zyon.Game/Zyon.Game.csproj` (0 avisos) e `tools/smoke-test.sh`.
- **Arte é ajustável:** os números de desenho (tons, ângulos, tamanhos) deste plano são um ponto de partida. Se, ao olhar o PNG gerado, algo ficar feio, ajuste o desenho — mas mantenha os formatos (tamanhos de célula, ordem dos quadros, nomes de arquivo) e os testes.
- **Determinismo:** desenho só com inteiros, hash próprio e `DeterministicMath` (`Zyon.Core.WorldGen`); nada de `System.Random`, `MathF.Sin/Cos`.

## Formatos (contrato entre ArtGen, Client e Game)

- Pasta: `data/client/art/`. Arquivos: `tiles.png`, `walls.png`, `merge.png`, `items.png`, `player/body.png`, `player/outfit.png`, `player/hair.png`, `player/arm.png`, `art.json`.
- **tiles.png / walls.png:** células 16×16. Linha = material (a ordem dos blocos/paredes do registro, sem o ar: linha 0 = id 1). Coluna = `máscara × variações + variação` (máscara do `AutoTile`: bit N=1, L=2, S=4, O=8 = vizinho conectado). Blocos não sólidos (tocha) repetem o mesmo sprite em todas as células.
- **merge.png:** células 16×16. Linha = posição do material na `mergeOrder`. Coluna = `lado × variações + variação`, lado N=0, L=1, S=2, O=3 = lado do tile **duro** em que a borda do material mole é desenhada (o vizinho mole está daquele lado).
- **items.png:** uma linha de células 16×16; célula = ordem dos itens no registro sem o "nenhum" (célula 0 = item id 1).
- **player/*.png:** tira horizontal de quadros 32×48. `body`, `outfit`, `hair`: 10 quadros (0 parado, 1–8 andando, 9 no ar). `arm`: 15 quadros (0–9 iguais aos de cima, 10–14 golpe). Pés no ponto (16, 47) do quadro.
- **Ângulos:** "ângulo de tela" em graus, a partir de +X, Y para baixo (o mesmo de `RotationDegrees` do Godot). `pointDeg` de um item = direção para onde o ícone "aponta" (picareta desenhada na diagonal: −45; bloco/tocha: −90).

## Mapa de arquivos

```
art/art.json                                            # novo — entrada do gerador
data/client/art/*                                       # novo — saída do gerador (commitada)
data/client/visuals.json                                # (mod) só "sky"
tools/gen-art.sh                                        # novo
src/Zyon.Core/Entities/EntityView.cs, src/Zyon.Core/Sim/Simulation.cs        # (mod) campos novos
src/Zyon.ArtGen/  Zyon.ArtGen.csproj Program.cs Rgba.cs Canvas.cs Png.cs ArtHash.cs
                  ArtSource.cs TileGenerator.cs ItemIconGenerator.cs PlayerGenerator.cs ArtBuilder.cs
tests/Zyon.ArtGen.Tests/  Zyon.ArtGen.Tests.csproj PngTests.cs TileGeneratorTests.cs ItemAndPlayerTests.cs ArtUpToDateTests.cs RepoPaths.cs
src/Zyon.Client/  ArtManifest.cs ArtCatalog.cs MergeAtlasLayout.cs PlayerAnimator.cs           # novos
src/Zyon.Client/  ChunkMeshBuilder.cs ClientVisuals.cs                                          # (mod)
src/Zyon.Client/PlaceholderAtlas.cs (+ teste)                                                   # removidos
src/Zyon.Game/Rendering/ ArtTextures.cs PlayerNode.cs ItemIcons.cs                              # novos
src/Zyon.Game/ GameFlow.cs GameRoot.cs SmokeTest.cs Rendering/WorldRenderer.cs Rendering/ChunkNode.cs Rendering/EntityRenderer.cs Hud/Hotbar.cs Hud/InventorySlot.cs  # (mod)
Zyon.slnx, CLAUDE.md                                                                            # (mod)
```

---

### Task 1: Estado de animação no `EntityView` (Core)

**Files:**
- Modify: `src/Zyon.Core/Entities/EntityView.cs`, `src/Zyon.Core/Sim/Simulation.cs` (`MakeView`)
- Test: `tests/Zyon.Core.Tests/EntityViewTests.cs`

- [ ] **Step 1: Teste que falha**

`tests/Zyon.Core.Tests/EntityViewTests.cs`:

```csharp
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class EntityViewTests
{
    [Fact]
    public void Player_ExposesAnimationState()
    {
        var h = new SimHarness();
        ushort pickaxe = h.Registry.ItemId("basic_pickaxe");
        h.Sim.TryGetEntityView(h.PlayerId, out var standing);
        Assert.Equal(pickaxe, standing.HeldItemId);     // slot 0 selecionado com a picareta
        Assert.False(standing.UsingItem);

        // Anda para a direita segurando o botão de uso mirando 3 tiles à frente.
        var p = standing.Position;
        for (int i = 0; i < 30; i++)
            h.Step(new PlayerInput(1f, false, true, (int)p.X + 3, (int)p.Y));
        h.Sim.TryGetEntityView(h.PlayerId, out var moving);

        Assert.True(moving.Velocity.X > 0);
        Assert.True(moving.OnGround);
        Assert.Equal(1, moving.Facing);
        Assert.True(moving.UsingItem);
        Assert.Equal((int)p.X + 3, moving.AimX);
        Assert.Equal((int)p.Y, moving.AimY);
    }

    [Fact]
    public void DroppedItem_HasNoPlayerState()
    {
        var h = new SimHarness();
        h.Sim.Send(new DropItemCommand(h.PlayerId, 0, 1));
        h.Step(default);
        var items = new List<EntityView>();
        h.Sim.CopyEntities(items);
        var item = Assert.Single(items, e => e.Kind == EntityKind.DroppedItem);

        Assert.Equal((ushort)0, item.HeldItemId);
        Assert.False(item.UsingItem);
    }
}
```

(Adapte ao `SimHarness` real: o nome do método que roda um tick com input — `Step`, `Tick`, `Run` — e o comando de soltar item. Se o jogador do harness não começar no chão, rode alguns ticks parados antes. Se `Facing` usar outra convenção que ±1, use a do Core e documente no `EntityView`.)

Run: `dotnet test --filter "FullyQualifiedName~EntityViewTests"` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Core/Entities/EntityView.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Items;

namespace Zyon.Core.Entities;

/// <summary>
/// Cópia somente leitura do estado de uma entidade para a apresentação. Os campos depois de Item só têm
/// valor para jogadores: velocidade (tiles/s), no chão, lado (+1 direita, −1 esquerda), botão de uso
/// pressionado, tile mirado e item selecionado na hotbar (0 = mão vazia).
/// </summary>
public readonly record struct EntityView(
    int NetworkId,
    EntityKind Kind,
    Vector2 Position,
    Vector2 PreviousPosition,
    Vector2 Size,
    ItemStack Item,
    Vector2 Velocity = default,
    bool OnGround = false,
    int Facing = 1,
    bool UsingItem = false,
    int AimX = 0,
    int AimY = 0,
    ushort HeldItemId = 0);
```

`MakeView` em `Simulation.cs`:

```csharp
    private EntityView MakeView(Entity e)
    {
        var position = Ecs.Get<Position>(e).Value;
        var previous = Ecs.Get<PreviousPosition>(e).Value;
        ref var body = ref Ecs.Get<Body>(e);
        int id = Ecs.Get<NetworkId>(e).Value;
        if (!Ecs.Has<PlayerState>(e))
            return new EntityView(id, EntityKind.DroppedItem, position, previous, body.Size, Ecs.Get<DroppedItem>(e).Stack);

        ref var state = ref Ecs.Get<PlayerState>(e);
        var inventory = Ecs.Get<InventoryRef>(e).Inventory;
        return new EntityView(id, EntityKind.Player, position, previous, body.Size, ItemStack.Empty,
            Ecs.Get<Velocity>(e).Value, body.OnGround, state.Facing, state.Input.Primary,
            state.Input.AimX, state.Input.AimY, inventory.Get(state.SelectedSlot).ItemId);
    }
```

(`inventory.Get(slot)` — use o método real de leitura de slot do `Inventory`. Se `Facing` puder ser 0 antes do primeiro movimento, normalize para 1.)

- [ ] **Step 3: Rodar** — `dotnet test` → verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Core tests/Zyon.Core.Tests/EntityViewTests.cs
git commit -m "feat(core): EntityView expõe estado para animação do jogador" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 2: Projeto ArtGen — cores, tela, hash e PNG

**Files:**
- Create: `src/Zyon.ArtGen/Zyon.ArtGen.csproj`, `src/Zyon.ArtGen/Rgba.cs`, `src/Zyon.ArtGen/Canvas.cs`, `src/Zyon.ArtGen/ArtHash.cs`, `src/Zyon.ArtGen/Png.cs`, `src/Zyon.ArtGen/Program.cs` (provisório)
- Create: `tests/Zyon.ArtGen.Tests/Zyon.ArtGen.Tests.csproj`, `tests/Zyon.ArtGen.Tests/PngTests.cs`, `tests/Zyon.ArtGen.Tests/RepoPaths.cs`
- Modify: `Zyon.slnx`

- [ ] **Step 1: Projetos**

`src/Zyon.ArtGen/Zyon.ArtGen.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Zyon.Core\Zyon.Core.csproj" />
    <ProjectReference Include="..\Zyon.Client\Zyon.Client.csproj" />
  </ItemGroup>
</Project>
```

`tests/Zyon.ArtGen.Tests/Zyon.ArtGen.Tests.csproj`: copie o `tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj` (mesmos pacotes xUnit e versões), troque a referência para `..\..\src\Zyon.ArtGen\Zyon.ArtGen.csproj` e **não** copie a pasta `data` (os testes leem o repositório direto via `RepoPaths`).

`Zyon.slnx`: adicionar `src/Zyon.ArtGen/Zyon.ArtGen.csproj` em `/src/` e `tests/Zyon.ArtGen.Tests/Zyon.ArtGen.Tests.csproj` em `/tests/`.

`src/Zyon.ArtGen/Program.cs` (provisório, a Task 5 completa):

```csharp
Console.WriteLine("Zyon.ArtGen");
```

`tests/Zyon.ArtGen.Tests/RepoPaths.cs`:

```csharp
namespace Zyon.ArtGen.Tests;

/// <summary>Raiz do repositório: sobe a partir da pasta do teste até achar o Zyon.slnx.</summary>
public static class RepoPaths
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Zyon.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Zyon.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }
}
```

- [ ] **Step 2: Testes que falham**

`tests/Zyon.ArtGen.Tests/PngTests.cs`:

```csharp
namespace Zyon.ArtGen.Tests;

public class PngTests
{
    private static Canvas Sample()
    {
        var c = new Canvas(5, 3);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 5; x++)
            c.Set(x, y, new Rgba((byte)(x * 50), (byte)(y * 100), (byte)(x + y), (byte)(x == 0 ? 0 : 255)));
        return c;
    }

    [Fact]
    public void EncodeThenDecode_GivesTheSamePixels()
    {
        var original = Sample();

        var back = Png.Decode(Png.Encode(original));

        Assert.Equal(5, back.Width);
        Assert.Equal(3, back.Height);
        Assert.Equal(original.Pixels, back.Pixels);
    }

    [Fact]
    public void Encode_StartsWithPngSignature_AndIsDeterministic()
    {
        var a = Png.Encode(Sample());
        var b = Png.Encode(Sample());

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, a[..8]);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Decode_RejectsGarbage()
    {
        Assert.Throws<InvalidDataException>(() => Png.Decode([1, 2, 3]));
    }

    [Fact]
    public void Rgba_ParsesHex_AndScalesWithClamp()
    {
        var c = Rgba.Parse("#804020");

        Assert.Equal(new Rgba(128, 64, 32), c);
        Assert.Equal(new Rgba(192, 96, 48), c.Scale(1.5f));
        Assert.Equal(new Rgba(255, 160, 80), c.Scale(2.5f));   // 320 satura em 255
        Assert.Throws<FormatException>(() => Rgba.Parse("804020"));
    }

    [Fact]
    public void Hash_IsStable()
    {
        Assert.Equal(ArtHash.Get(3, 7, 11), ArtHash.Get(3, 7, 11));
        Assert.NotEqual(ArtHash.Get(3, 7, 11), ArtHash.Get(4, 7, 11));
        Assert.InRange(ArtHash.Range(3, 7, 11, 5), 0, 4);
    }
}
```

Run: `dotnet test tests/Zyon.ArtGen.Tests` → FAIL na compilação.

- [ ] **Step 3: Implementar**

`src/Zyon.ArtGen/Rgba.cs`:

```csharp
using System.Globalization;

namespace Zyon.ArtGen;

public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    public static Rgba Parse(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' ||
            !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint v))
            throw new FormatException($"cor inválida '{hex}' (use #rrggbb)");
        return new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    /// <summary>Multiplica R, G e B (alpha fica igual), saturando em 0..255.</summary>
    public Rgba Scale(float f) => new(Channel(R, f), Channel(G, f), Channel(B, f), A);

    private static byte Channel(byte v, float f) => (byte)Math.Clamp((int)MathF.Round(v * f), 0, 255);
}
```

`src/Zyon.ArtGen/Canvas.cs`:

```csharp
namespace Zyon.ArtGen;

/// <summary>Imagem RGBA 8 bits, linha por linha. Começa transparente.</summary>
public sealed class Canvas
{
    public Canvas(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Rgba Get(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    /// <summary>Escreve o pixel; fora da imagem é ignorado.</summary>
    public void Set(int x, int y, Rgba c)
    {
        if (!Contains(x, y)) return;
        int i = (y * Width + x) * 4;
        Pixels[i] = c.R;
        Pixels[i + 1] = c.G;
        Pixels[i + 2] = c.B;
        Pixels[i + 3] = c.A;
    }

    /// <summary>Copia um retângulo de outra imagem (inclusive pixels transparentes).</summary>
    public void Blit(Canvas source, int sx, int sy, int width, int height, int dx, int dy)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            Set(dx + x, dy + y, source.Get(sx + x, sy + y));
    }

    /// <summary>Contorno: todo pixel transparente vizinho (4 lados) de um opaco, dentro do retângulo, vira a cor dada.</summary>
    public void Outline(int x0, int y0, int width, int height, Rgba color)
    {
        var marks = new List<(int, int)>();
        for (int y = y0; y < y0 + height; y++)
        for (int x = x0; x < x0 + width; x++)
        {
            if (Get(x, y).A != 0) continue;
            bool near = (x > x0 && Get(x - 1, y).A != 0) || (x < x0 + width - 1 && Get(x + 1, y).A != 0)
                     || (y > y0 && Get(x, y - 1).A != 0) || (y < y0 + height - 1 && Get(x, y + 1).A != 0);
            if (near) marks.Add((x, y));
        }
        foreach (var (x, y) in marks) Set(x, y, color);
    }
}
```

`src/Zyon.ArtGen/ArtHash.cs`:

```csharp
namespace Zyon.ArtGen;

/// <summary>Hash inteiro estável (mesmo resultado em qualquer plataforma e versão do .NET).</summary>
public static class ArtHash
{
    public static uint Get(int x, int y, int seed)
    {
        uint h = unchecked((uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u);
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        return h ^ (h >> 16);
    }

    /// <summary>Inteiro em [0, count).</summary>
    public static int Range(int x, int y, int seed, int count) => (int)(Get(x, y, seed) % (uint)count);

    /// <summary>Verdadeiro em <paramref name="percent"/>% dos casos.</summary>
    public static bool Chance(int x, int y, int seed, int percent) => Get(x, y, seed) % 100u < (uint)percent;
}
```

`src/Zyon.ArtGen/Png.cs`:

```csharp
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Zyon.ArtGen;

/// <summary>PNG RGBA 8 bits. Grava sem filtro; lê os 5 filtros do formato.</summary>
public static class Png
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(Canvas image)
    {
        using var output = new MemoryStream();
        output.Write(Signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;   // bits por canal
        header[9] = 6;   // RGBA
        WriteChunk(output, "IHDR", header);

        int stride = image.Width * 4;
        var raw = new byte[image.Height * (stride + 1)];
        for (int y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * stride, stride).CopyTo(raw.AsSpan(y * (stride + 1) + 1));   // filtro 0
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
                z.Write(raw);
            WriteChunk(output, "IDAT", compressed.ToArray());
        }
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    public static Canvas Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("não é um PNG");
        int pos = 8, width = 0, height = 0;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png.AsSpan(pos + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[9] != 6 || data[12] != 0)
                    throw new InvalidDataException("só PNG RGBA 8 bits sem entrelaçamento");
            }
            else if (type == "IDAT") idat.Write(data);
            else if (type == "IEND") break;
            pos += 12 + length;
        }
        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG sem IHDR");

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = width * 4;
        var raw = new byte[height * (stride + 1)];
        z.ReadExactly(raw);

        var image = new Canvas(width, height);
        var prev = new byte[stride];
        var line = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[y * (stride + 1)];
            raw.AsSpan(y * (stride + 1) + 1, stride).CopyTo(line);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= 4 ? line[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
                line[i] = filter switch
                {
                    0 => line[i],
                    1 => (byte)(line[i] + a),
                    2 => (byte)(line[i] + b),
                    3 => (byte)(line[i] + (a + b) / 2),
                    4 => (byte)(line[i] + Paeth(a, b, c)),
                    _ => throw new InvalidDataException($"filtro PNG desconhecido {filter}"),
                };
            }
            line.CopyTo(image.Pixels, y * stride);
            (prev, line) = (line, prev);
        }
        return image;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        output.Write(buf);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc);
        output.Write(buf);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
```

- [ ] **Step 4: Rodar** — `dotnet test` → verde (os três projetos de teste).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.ArtGen tests/Zyon.ArtGen.Tests Zyon.slnx
git commit -m "feat(artgen): projeto do gerador de arte com PNG próprio" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 3: Manifesto e catálogo de arte (Client)

**Files:**
- Create: `src/Zyon.Client/ArtManifest.cs`, `src/Zyon.Client/ArtCatalog.cs`
- Test: `tests/Zyon.Client.Tests/ArtCatalogTests.cs`

O manifesto é o `art.json` gravado pelo ArtGen. O catálogo é o manifesto resolvido contra o `Registry` (arrays por id) e validado.

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Client.Tests/ArtCatalogTests.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ArtCatalogTests
{
    private static ArtManifest Complete()
    {
        var r = TestData.Registry();
        var m = new ArtManifest();
        for (int i = 1; i < r.Blocks.Count; i++) m.Tiles[r.Blocks[i].Name] = i - 1;
        for (int i = 1; i < r.Walls.Count; i++) m.Walls[r.Walls[i].Name] = i - 1;
        m.MergeOrder.AddRange(["dirt", "stone", "deep_stone", "ore_a", "ore_b"]);
        for (int i = 1; i < r.Items.Count; i++)
            m.Items[r.Items[i].Name] = new ItemArt { Cell = i - 1, GripX = 8, GripY = 14, PointDeg = -90 };
        m.Player.Hand.AddRange(Enumerable.Repeat(new[] { 20, 28 }, m.Player.ArmFrames));
        m.Player.ArmAngleDeg.AddRange(Enumerable.Repeat(90f, m.Player.ArmFrames));
        return m;
    }

    [Fact]
    public void Resolve_MapsNamesToIds()
    {
        var r = TestData.Registry();

        var c = ArtCatalog.Resolve(Complete(), r);

        Assert.Equal(0, c.TileRowByBlockId[r.BlockId("dirt")]);
        Assert.Equal(-1, c.TileRowByBlockId[0]);
        Assert.Equal(1, c.MergeRankByBlockId[r.BlockId("stone")]);
        Assert.Equal(-1, c.MergeRankByBlockId[r.BlockId("torch")]);
        Assert.Equal(r.Items.Count - 2, c.ItemByItemId[r.ItemId("torch")]!.Cell);
    }

    [Fact]
    public void MissingBlockArt_Throws()
    {
        var m = Complete();
        m.Tiles.Remove("torch");

        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry()));

        Assert.Contains("falta a arte do bloco 'torch'", ex.Message);
    }

    [Theory]
    [InlineData("wall")]
    [InlineData("item")]
    public void MissingWallOrItemArt_Throws(string what)
    {
        var m = Complete();
        if (what == "wall") m.Walls.Remove("stone_wall"); else m.Items.Remove("basic_pickaxe");

        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry()));

        Assert.Contains(what == "wall" ? "parede 'stone_wall'" : "item 'basic_pickaxe'", ex.Message);
    }

    [Fact]
    public void UnknownName_OrNonSolidInMergeOrder_Throws()
    {
        var m = Complete();
        m.MergeOrder.Add("torch");
        Assert.Contains("mergeOrder", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);

        m = Complete();
        m.Tiles["unobtainium"] = 9;
        Assert.Contains("unobtainium", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Fact]
    public void WrongPlayerFrameData_Throws()
    {
        var m = Complete();
        m.Player.Hand.RemoveAt(0);

        Assert.Contains("hand", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var m = Complete();

        var back = ArtManifest.FromJson(m.ToJson());

        Assert.Equal(m.ToJson(), back.ToJson());
        Assert.Contains("\"mergeOrder\"", m.ToJson());
    }

    [Fact]
    public void LoadFromDirectory_MissingPng_ThrowsWithPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "zyon-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "client", "art"));
        File.WriteAllText(Path.Combine(dir, "client", "art", ArtManifest.FileName), Complete().ToJson());
        try
        {
            var ex = Assert.Throws<DataException>(() => ArtCatalog.LoadFromDirectory(dir, TestData.Registry()));
            Assert.Contains("tiles.png", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/ArtManifest.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Data;

namespace Zyon.Client;

/// <summary>Ponto de encaixe (pixels no ícone 16×16) e direção para onde o ícone aponta (ângulo de tela, graus).</summary>
public sealed class ItemArt
{
    public int Cell { get; set; }
    public int GripX { get; set; }
    public int GripY { get; set; }
    public float PointDeg { get; set; }
}

/// <summary>Quadros do jogador (tiras de 32×48; pés em FeetX, FeetY). Ver "Formatos" no plano 6.</summary>
public sealed class PlayerArt
{
    public int FrameWidth { get; set; } = 32;
    public int FrameHeight { get; set; } = 48;
    public int FeetX { get; set; } = 16;
    public int FeetY { get; set; } = 47;
    public List<string> Layers { get; set; } = ["body", "outfit", "hair"];
    public int BodyFrames { get; set; } = 10;
    public int ArmFrames { get; set; } = 15;
    public int Idle { get; set; } = 0;
    public int WalkFirst { get; set; } = 1;
    public int WalkCount { get; set; } = 8;
    public int Air { get; set; } = 9;
    public int SwingFirst { get; set; } = 10;
    public int SwingCount { get; set; } = 5;

    /// <summary>Ponto da mão em cada quadro do braço (x, y no quadro).</summary>
    public List<int[]> Hand { get; set; } = [];

    /// <summary>Direção do braço (ombro → mão) em cada quadro, em ângulo de tela.</summary>
    public List<float> ArmAngleDeg { get; set; } = [];
}

/// <summary>data/client/art/art.json — escrito pelo Zyon.ArtGen, lido pelo jogo.</summary>
public sealed class ArtManifest
{
    public const string FileName = "art.json";
    public const string TilesFile = "tiles.png";
    public const string WallsFile = "walls.png";
    public const string MergeFile = "merge.png";
    public const string ItemsFile = "items.png";
    public const string ArmLayer = "arm";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public int TileSize { get; set; } = 16;
    public int Variations { get; set; } = 3;
    public Dictionary<string, int> Tiles { get; set; } = [];
    public Dictionary<string, int> Walls { get; set; } = [];
    public List<string> MergeOrder { get; set; } = [];
    public Dictionary<string, ItemArt> Items { get; set; } = [];
    public PlayerArt Player { get; set; } = new();

    public static string PlayerFile(string layer) => $"player/{layer}.png";

    public string ToJson() => JsonSerializer.Serialize(this, Options).ReplaceLineEndings("\n") + "\n";

    public static ArtManifest FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ArtManifest>(json, Options) ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}", ex);
        }
    }
}
```

`src/Zyon.Client/ArtCatalog.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Client;

/// <summary>Manifesto de arte resolvido por id. Toda peça de conteúdo dos dados precisa de arte.</summary>
public sealed class ArtCatalog
{
    private ArtCatalog(ArtManifest manifest, string directory, int[] tileRows, int[] wallRows, int[] mergeRanks, ItemArt?[] items)
    {
        Manifest = manifest;
        Directory = directory;
        TileRowByBlockId = tileRows;
        WallRowByWallId = wallRows;
        MergeRankByBlockId = mergeRanks;
        ItemByItemId = items;
    }

    public ArtManifest Manifest { get; }

    /// <summary>Pasta dos PNGs (vazia se resolvido sem disco).</summary>
    public string Directory { get; }

    public IReadOnlyList<int> TileRowByBlockId { get; }
    public IReadOnlyList<int> WallRowByWallId { get; }

    /// <summary>Posição na mergeOrder (menor = mais mole); −1 = não mescla.</summary>
    public IReadOnlyList<int> MergeRankByBlockId { get; }

    public IReadOnlyList<ItemArt?> ItemByItemId { get; }

    public string PathOf(string file) => Path.Combine(Directory, file.Replace('/', Path.DirectorySeparatorChar));

    public static ArtCatalog LoadFromDirectory(string dataDir, Registry registry)
    {
        string dir = Path.Combine(dataDir, "client", "art");
        string path = Path.Combine(dir, ArtManifest.FileName);
        if (!File.Exists(path)) throw new DataException($"{ArtManifest.FileName}: arquivo não encontrado em '{path}' (rode tools/gen-art.sh)");
        var catalog = Resolve(ArtManifest.FromJson(File.ReadAllText(path)), registry, dir);
        var files = new List<string> { ArtManifest.TilesFile, ArtManifest.WallsFile, ArtManifest.MergeFile, ArtManifest.ItemsFile };
        files.AddRange(catalog.Manifest.Player.Layers.Select(ArtManifest.PlayerFile));
        files.Add(ArtManifest.PlayerFile(ArtManifest.ArmLayer));
        foreach (var f in files)
            if (!File.Exists(catalog.PathOf(f))) throw new DataException($"arte: arquivo '{catalog.PathOf(f)}' não encontrado");
        return catalog;
    }

    public static ArtCatalog Resolve(ArtManifest m, Registry registry, string directory = "")
    {
        static DataException Fail(string message) => new($"{ArtManifest.FileName}: {message}");

        var tileRows = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        foreach (var (name, row) in m.Tiles)
            tileRows[registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' não existe")] = row;
        for (int id = 1; id < tileRows.Length; id++)
            if (tileRows[id] < 0) throw Fail($"falta a arte do bloco '{registry.Block((ushort)id).Name}'");

        var wallRows = Enumerable.Repeat(-1, registry.Walls.Count).ToArray();
        foreach (var (name, row) in m.Walls)
            wallRows[registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' não existe")] = row;
        for (int id = 1; id < wallRows.Length; id++)
            if (wallRows[id] < 0) throw Fail($"falta a arte da parede '{registry.Wall((ushort)id).Name}'");

        var ranks = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        for (int i = 0; i < m.MergeOrder.Count; i++)
        {
            string name = m.MergeOrder[i];
            if (!registry.TryGetBlockId(name, out var id)) throw Fail($"mergeOrder: bloco '{name}' não existe");
            if (!registry.Block(id).Solid) throw Fail($"mergeOrder: bloco '{name}' não é sólido");
            if (ranks[id] >= 0) throw Fail($"mergeOrder: bloco '{name}' repetido");
            ranks[id] = i;
        }

        var items = new ItemArt?[registry.Items.Count];
        foreach (var (name, art) in m.Items)
            items[registry.TryGetItemId(name, out var id) ? id : throw Fail($"item '{name}' não existe")] = art;
        for (int id = 1; id < items.Length; id++)
            if (items[id] is null) throw Fail($"falta a arte do item '{registry.Item((ushort)id).Name}'");

        var p = m.Player;
        if (p.Hand.Count != p.ArmFrames || p.Hand.Any(h => h.Length != 2))
            throw Fail($"player.hand precisa de {p.ArmFrames} pontos (x, y)");
        if (p.ArmAngleDeg.Count != p.ArmFrames) throw Fail($"player.armAngleDeg precisa de {p.ArmFrames} valores");
        if (p.SwingFirst + p.SwingCount > p.ArmFrames || p.WalkFirst + p.WalkCount > p.BodyFrames || p.Air >= p.BodyFrames)
            throw Fail("player: índices de quadro fora das tiras");

        return new ArtCatalog(m, directory, tileRows, wallRows, ranks, items);
    }
}
```

- [ ] **Step 3: Rodar** — `dotnet test` → verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests/ArtCatalogTests.cs
git commit -m "feat(client): manifesto e catálogo de arte validados contra os dados" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 4: Entrada do gerador e arte dos blocos, paredes e mescla

**Files:**
- Create: `art/art.json`, `src/Zyon.ArtGen/ArtSource.cs`, `src/Zyon.ArtGen/TileGenerator.cs`
- Test: `tests/Zyon.ArtGen.Tests/TileGeneratorTests.cs`

- [ ] **Step 1: `art/art.json`**

```json
{
  "seed": 7,
  "variations": 3,
  "blocks": {
    "dirt":       { "recipe": "clumps", "palette": ["#4a3a2c", "#6b5440", "#85694f"], "cap": ["#5c5850", "#7d776b", "#969084"] },
    "stone":      { "recipe": "clumps", "palette": ["#55554f", "#6f6f6c", "#8a8a84"] },
    "deep_stone": { "recipe": "panels", "palette": ["#2c313d", "#3d4452", "#525b6d"] },
    "ore_a":      { "recipe": "ore", "base": "stone", "fleck": ["#7a3f1f", "#b0643a", "#d88a55"] },
    "ore_b":      { "recipe": "ore", "base": "deep_stone", "fleck": ["#2f8f80", "#6fe0c8", "#c8fff2"] },
    "torch":      { "recipe": "torch", "palette": ["#3e2a1a", "#5e4228", "#7a5a38"], "flame": ["#ff8a2a", "#ffc65a", "#fff2c0"] }
  },
  "walls": {
    "dirt_wall":  { "recipe": "clumps", "palette": ["#2a2018", "#3a2d22", "#46372a"] },
    "stone_wall": { "recipe": "clumps", "palette": ["#2c2c2a", "#3a3a37", "#464642"] }
  },
  "mergeOrder": ["dirt", "stone", "deep_stone", "ore_a", "ore_b"],
  "player": {
    "skin": ["#9a6a4a", "#c08a64", "#d8a880"],
    "hair": ["#2a1e18", "#3e2c22", "#5a4232"],
    "outfit": ["#24525e", "#2f6f7f", "#4a95a6"],
    "detail": "#d9a441",
    "boots": ["#26221e", "#3a332c"],
    "outline": "#14161a"
  },
  "items": {
    "basic_pickaxe": { "recipe": "pickaxe", "handle": ["#4a3420", "#6e5032", "#8f6c46"], "head": ["#5a5f66", "#8a9099", "#c4cad2"] }
  }
}
```

(Itens que colocam bloco não precisam de entrada: usam a receita `block`. `ore_b` usa a base `deep_stone` — o minério B fica no fundo.)

- [ ] **Step 2: Testes que falham**

`tests/Zyon.ArtGen.Tests/TileGeneratorTests.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen.Tests;

public class TileGeneratorTests
{
    private static readonly Registry R = RegistryLoader.LoadFromDirectory(Path.Combine(RepoPaths.Root, "data"));
    private static readonly ArtSource Source = ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);
    private const int T = 16;

    private static (int X, int Y) Cell(int row, int mask, int variation) =>
        ((mask * Source.Variations + variation) * T, row * T);

    [Fact]
    public void Blocks_HaveOneRowPerBlock_AndAllCells()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);

        Assert.Equal(AutoTile.MaskCount * Source.Variations * T, img.Width);
        Assert.Equal((R.Blocks.Count - 1) * T, img.Height);
        Assert.Equal(0, rows["dirt"]);
        Assert.Equal(R.Blocks.Count - 2, rows["torch"]);
    }

    [Fact]
    public void IsolatedBlock_HasOutline_AndCutCorners()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["stone"], mask: 0, variation: 0);
        var outline = Rgba.Parse(Source.Blocks["stone"].Palette![0]).Scale(TileGenerator.OutlineFactor);

        Assert.Equal(0, img.Get(x, y).A);                 // canto arredondado
        Assert.Equal(0, img.Get(x + 15, y + 15).A);
        Assert.Equal(outline, img.Get(x + 8, y));         // contorno no topo
        Assert.Equal(outline, img.Get(x, y + 8));         // contorno à esquerda
    }

    [Fact]
    public void FullyConnectedBlock_HasNoOutline_AndIsOpaque()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["stone"], mask: 15, variation: 1);
        var outline = Rgba.Parse(Source.Blocks["stone"].Palette![0]).Scale(TileGenerator.OutlineFactor);

        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            Assert.Equal(255, img.Get(x + px, y + py).A);
            Assert.NotEqual(outline, img.Get(x + px, y + py));
        }
    }

    [Fact]
    public void DirtWithExposedTop_HasAshCap()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["dirt"], mask: AutoTile.East | AutoTile.South | AutoTile.West, variation: 0);
        var cap = Source.Blocks["dirt"].Cap!.Select(Rgba.Parse).ToArray();

        Assert.Contains(img.Get(x + 8, y + 2), cap.Append(cap[1].Scale(TileGenerator.HighlightFactor)).Append(cap[0]).Append(cap[2]));
    }

    [Fact]
    public void Torch_IsTheSameInEveryCell_AndTransparentAround()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x0, y0) = Cell(rows["torch"], 0, 0);
        var (x1, y1) = Cell(rows["torch"], 15, 2);

        Assert.Equal(0, img.Get(x0, y0).A);
        Assert.Equal(255, img.Get(x0 + 7, y0 + 10).A);
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
            Assert.Equal(img.Get(x0 + px, y0 + py), img.Get(x1 + px, y1 + py));
    }

    [Fact]
    public void Ore_HasFleckColors()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["ore_b"], 15, 0);
        var fleck = Source.Blocks["ore_b"].Fleck!.Select(Rgba.Parse).ToHashSet();

        int count = 0;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
            if (fleck.Contains(img.Get(x + px, y + py))) count++;
        Assert.InRange(count, 6, 60);
    }

    [Fact]
    public void Merge_IsTransparentExceptA2To4PixelBandOnItsSide()
    {
        var img = TileGenerator.BuildMerge(Source, R);
        int row = Source.MergeOrder.IndexOf("dirt");
        for (int side = 0; side < 4; side++)
        for (int v = 0; v < Source.Variations; v++)
        {
            int x = (side * Source.Variations + v) * T, y = row * T;
            for (int along = 0; along < T; along++)
            {
                int depth = 0;
                for (int d = 0; d < T; d++)
                {
                    var (px, py) = side switch { 0 => (along, d), 1 => (T - 1 - d, along), 2 => (along, T - 1 - d), _ => (d, along) };
                    if (img.Get(x + px, y + py).A != 0) depth = d + 1;
                }
                Assert.InRange(depth, 2, 4);
            }
        }
    }

    [Fact]
    public void Walls_HaveOneRowPerWall_Opaque()
    {
        var img = TileGenerator.BuildWalls(Source, R, out var rows);

        Assert.Equal((R.Walls.Count - 1) * T, img.Height);
        Assert.Equal(1, rows["stone_wall"]);
        var (x, y) = Cell(rows["dirt_wall"], 0, 0);
        Assert.Equal(255, img.Get(x, y).A);
    }

    [Fact]
    public void SameInput_SamePixels()
    {
        var a = TileGenerator.BuildBlocks(Source, R, out _);
        var b = TileGenerator.BuildBlocks(Source, R, out _);

        Assert.Equal(a.Pixels, b.Pixels);
    }

    [Theory]
    [InlineData("""{"seed":1,"variations":3,"blocks":{},"walls":{},"mergeOrder":[],"player":{"skin":["#000000","#000000","#000000"],"hair":["#000000","#000000","#000000"],"outfit":["#000000","#000000","#000000"],"detail":"#000000","boots":["#000000","#000000"],"outline":"#000000"},"items":{}}""", "falta a arte do bloco 'dirt'")]
    [InlineData("""{"seed":1,"variations":0}""", "variations")]
    public void InvalidSource_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => ArtSource.Load(json, R));

        Assert.Contains("art.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }
}
```

Run: `dotnet test tests/Zyon.ArtGen.Tests` → FAIL na compilação.

- [ ] **Step 3: Implementar `ArtSource`**

`src/Zyon.ArtGen/ArtSource.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

public sealed class MaterialSource
{
    public string Recipe { get; set; } = "";
    public string[]? Palette { get; set; }
    public string[]? Cap { get; set; }
    public string? Base { get; set; }
    public string[]? Fleck { get; set; }
    public string[]? Flame { get; set; }
}

public sealed class PlayerSource
{
    public string[] Skin { get; set; } = [];
    public string[] Hair { get; set; } = [];
    public string[] Outfit { get; set; } = [];
    public string Detail { get; set; } = "";
    public string[] Boots { get; set; } = [];
    public string Outline { get; set; } = "";
}

public sealed class ItemSource
{
    public string Recipe { get; set; } = "";
    public string[]? Handle { get; set; }
    public string[]? Head { get; set; }
}

/// <summary>art/art.json — paletas e receitas da arte gerada. Validado contra o Registry.</summary>
public sealed class ArtSource
{
    public const string FileName = "art.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] SolidRecipes = ["clumps", "panels", "ore"];

    public int Seed { get; set; }
    public int Variations { get; set; } = 3;
    public Dictionary<string, MaterialSource> Blocks { get; set; } = [];
    public Dictionary<string, MaterialSource> Walls { get; set; } = [];
    public List<string> MergeOrder { get; set; } = [];
    public PlayerSource Player { get; set; } = new();
    public Dictionary<string, ItemSource> Items { get; set; } = [];

    public static ArtSource LoadFromFile(string path, Registry registry)
    {
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), registry);
    }

    public static ArtSource Load(string json, Registry registry)
    {
        ArtSource s;
        try
        {
            s = JsonSerializer.Deserialize<ArtSource>(json, Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        s.Validate(registry);
        return s;
    }

    /// <summary>Paleta de 3 tons de um bloco (para minério, a do material base).</summary>
    public Rgba[] BlockPalette(string name)
    {
        var m = Blocks[name];
        return Colors(m.Recipe == "ore" ? Blocks[m.Base!].Palette! : m.Palette!);
    }

    public static Rgba[] Colors(string[] hex) => hex.Select(Rgba.Parse).ToArray();

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private void Validate(Registry registry)
    {
        if (Variations < 1 || Variations > 8) throw Fail("'variations' deve estar entre 1 e 8");

        for (int id = 1; id < registry.Blocks.Count; id++)
        {
            var def = registry.Block((ushort)id);
            if (!Blocks.TryGetValue(def.Name, out var m)) throw Fail($"falta a arte do bloco '{def.Name}'");
            string ctx = $"blocks.{def.Name}";
            if (def.Solid && !SolidRecipes.Contains(m.Recipe)) throw Fail($"{ctx}: receita '{m.Recipe}' inválida para bloco sólido (use clumps, panels ou ore)");
            if (!def.Solid && m.Recipe != "torch") throw Fail($"{ctx}: bloco não sólido precisa da receita 'torch'");
            if (m.Recipe == "ore")
            {
                if (m.Base is null || !Blocks.TryGetValue(m.Base, out var b) || b.Recipe is not ("clumps" or "panels"))
                    throw Fail($"{ctx}: 'base' deve ser um bloco com receita clumps ou panels");
                CheckColors(m.Fleck, 3, $"{ctx}.fleck");
            }
            else CheckColors(m.Palette, 3, $"{ctx}.palette");
            if (m.Cap is not null) CheckColors(m.Cap, 3, $"{ctx}.cap");
            if (m.Recipe == "torch") CheckColors(m.Flame, 3, $"{ctx}.flame");
        }
        foreach (var name in Blocks.Keys)
            if (!registry.TryGetBlockId(name, out _)) throw Fail($"blocks: bloco '{name}' não existe");

        for (int id = 1; id < registry.Walls.Count; id++)
        {
            var def = registry.Wall((ushort)id);
            if (!Walls.TryGetValue(def.Name, out var m)) throw Fail($"falta a arte da parede '{def.Name}'");
            if (m.Recipe != "clumps") throw Fail($"walls.{def.Name}: paredes usam a receita 'clumps'");
            CheckColors(m.Palette, 3, $"walls.{def.Name}.palette");
        }
        foreach (var name in Walls.Keys)
            if (!registry.TryGetWallId(name, out _)) throw Fail($"walls: parede '{name}' não existe");

        var seen = new HashSet<string>();
        foreach (var name in MergeOrder)
        {
            if (!registry.TryGetBlockId(name, out var id) || !registry.Block(id).Solid) throw Fail($"mergeOrder: '{name}' não é um bloco sólido");
            if (!seen.Add(name)) throw Fail($"mergeOrder: '{name}' repetido");
        }

        CheckColors(Player.Skin, 3, "player.skin");
        CheckColors(Player.Hair, 3, "player.hair");
        CheckColors(Player.Outfit, 3, "player.outfit");
        CheckColors(Player.Boots, 2, "player.boots");
        CheckColors([Player.Detail, Player.Outline], 2, "player.detail/outline");

        for (int id = 1; id < registry.Items.Count; id++)
        {
            var def = registry.Item((ushort)id);
            if (Items.TryGetValue(def.Name, out var item))
            {
                if (item.Recipe != "pickaxe") throw Fail($"items.{def.Name}: receita '{item.Recipe}' desconhecida (use pickaxe; itens de bloco não precisam de entrada)");
                CheckColors(item.Handle, 3, $"items.{def.Name}.handle");
                CheckColors(item.Head, 3, $"items.{def.Name}.head");
            }
            else if (def.PlacesBlockId == 0) throw Fail($"falta a arte do item '{def.Name}'");
        }
        foreach (var name in Items.Keys)
            if (!registry.TryGetItemId(name, out _)) throw Fail($"items: item '{name}' não existe");
    }

    private static void CheckColors(string[]? colors, int count, string ctx)
    {
        if (colors is null || colors.Length != count) throw Fail($"'{ctx}' precisa de {count} cores");
        foreach (var c in colors)
        {
            try { Rgba.Parse(c); }
            catch (FormatException ex) { throw Fail($"'{ctx}': {ex.Message}"); }
        }
    }
}
```

- [ ] **Step 4: Implementar `TileGenerator`**

`src/Zyon.ArtGen/TileGenerator.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

/// <summary>
/// Atlas de blocos e paredes (16 máscaras × variações por linha) e de bordas de mescla (4 lados × variações).
/// Estilo contornado: manchas 2×2 em 3 tons; contorno, brilho e cantos arredondados só contra o vazio.
/// </summary>
public static class TileGenerator
{
    public const int T = 16;
    public const float OutlineFactor = 0.55f;
    public const float HighlightFactor = 1.18f;
    public const float ShadeFactor = 0.85f;

    public static Canvas BuildBlocks(ArtSource src, Registry registry, out Dictionary<string, int> rows)
    {
        rows = [];
        var img = new Canvas(AutoTile.MaskCount * src.Variations * T, (registry.Blocks.Count - 1) * T);
        for (int id = 1; id < registry.Blocks.Count; id++)
        {
            string name = registry.Block((ushort)id).Name;
            int row = id - 1;
            rows[name] = row;
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int v = 0; v < src.Variations; v++)
                DrawBlock(img, (mask * src.Variations + v) * T, row * T, src, name, mask, v, salt: id * 101);
        }
        return img;
    }

    public static Canvas BuildWalls(ArtSource src, Registry registry, out Dictionary<string, int> rows)
    {
        rows = [];
        var img = new Canvas(AutoTile.MaskCount * src.Variations * T, (registry.Walls.Count - 1) * T);
        for (int id = 1; id < registry.Walls.Count; id++)
        {
            string name = registry.Wall((ushort)id).Name;
            int row = id - 1;
            rows[name] = row;
            var pal = ArtSource.Colors(src.Walls[name].Palette!);
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int v = 0; v < src.Variations; v++)
                DrawWall(img, (mask * src.Variations + v) * T, row * T, pal, mask, v, src.Seed, salt: 5000 + id * 101);
        }
        return img;
    }

    /// <summary>Lado: 0 N, 1 L, 2 S, 3 O — lado do tile duro em que a borda do material mole aparece.</summary>
    public static Canvas BuildMerge(ArtSource src, Registry registry)
    {
        var img = new Canvas(4 * src.Variations * T, Math.Max(1, src.MergeOrder.Count) * T);
        for (int row = 0; row < src.MergeOrder.Count; row++)
        {
            string name = src.MergeOrder[row];
            var pal = src.BlockPalette(name);
            int salt = 9000 + registry.BlockId(name) * 101;
            for (int side = 0; side < 4; side++)
            for (int v = 0; v < src.Variations; v++)
                DrawTongue(img, (side * src.Variations + v) * T, row * T, pal, side, v, src.Seed, salt);
        }
        return img;
    }

    /// <summary>Tom 0..2 da mancha 2×2 que contém (px, py).</summary>
    public static int Tone(int px, int py, int v, int seed, int salt)
    {
        uint h = ArtHash.Get(v * 8 + px / 2, py / 2, seed * 31 + salt) % 100u;
        return h < 30 ? 0 : h >= 72 ? 2 : 1;
    }

    private static void DrawBlock(Canvas img, int ox, int oy, ArtSource src, string name, int mask, int v, int salt)
    {
        var m = src.Blocks[name];
        if (m.Recipe == "torch")
        {
            DrawTorch(img, ox, oy, ArtSource.Colors(m.Palette!), ArtSource.Colors(m.Flame!));
            return;
        }

        var pal = src.BlockPalette(name);
        var cap = m.Cap is null ? null : ArtSource.Colors(m.Cap);
        var fleck = m.Recipe == "ore" ? ArtSource.Colors(m.Fleck!) : null;
        bool panels = m.Recipe == "panels" || (m.Recipe == "ore" && src.Blocks[m.Base!].Recipe == "panels");
        bool n = (mask & AutoTile.North) == 0, e = (mask & AutoTile.East) == 0;
        bool s = (mask & AutoTile.South) == 0, w = (mask & AutoTile.West) == 0;

        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            if (CornerCut(px, py, n, e, s, w)) continue;
            var c = pal[Tone(px, py, v, src.Seed, salt)];
            if (panels) c = Panel(px, py, v, src.Seed, salt, pal) ?? c;
            if (fleck is not null) c = Fleck(px, py, v, src.Seed, salt, fleck) ?? c;
            bool capped = cap is not null && n && py < 2 + ArtHash.Range(v * T + px, 0, src.Seed + salt + 5, 2);
            if (capped) c = cap![Tone(px, py, v, src.Seed, salt + 7)];

            if ((n && py == 0) || (s && py == T - 1) || (w && px == 0) || (e && px == T - 1))
                c = (cap is not null && n && py == 0 ? cap[0] : pal[0]).Scale(OutlineFactor);
            else if (n && py == 1) c = c.Scale(HighlightFactor);
            else if ((w && px == 1) || (e && px == T - 2) || (s && py == T - 2)) c = c.Scale(ShadeFactor);
            img.Set(ox + px, oy + py, c);
        }
    }

    private static bool CornerCut(int px, int py, bool n, bool e, bool s, bool w) =>
        (n && w && px + py < 2) || (n && e && (T - 1 - px) + py < 2) ||
        (s && w && px + (T - 1 - py) < 2) || (s && e && (T - 1 - px) + (T - 1 - py) < 2);

    /// <summary>Linha de painel com bisel e uma emenda vertical (pista das ruínas high-tech).</summary>
    private static Rgba? Panel(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int line = 4 + ArtHash.Range(v, 0, seed + salt + 11, 8);
        int seam = 3 + ArtHash.Range(v, 1, seed + salt + 12, 10);
        if (px >= 2 && px <= T - 3 && py == line) return pal[0];
        if (px >= 2 && px <= T - 3 && py == line + 1) return pal[2];
        if (px == seam && py < line && py > 1) return pal[0];
        return null;
    }

    /// <summary>Três pintas em losango na cor do minério.</summary>
    private static Rgba? Fleck(int px, int py, int v, int seed, int salt, Rgba[] fleck)
    {
        for (int k = 0; k < 3; k++)
        {
            int cx = 3 + ArtHash.Range(v, k, seed + salt + 20, 10);
            int cy = 3 + ArtHash.Range(v, k, seed + salt + 21, 10);
            int d = Math.Abs(px - cx) + Math.Abs(py - cy);
            if (d == 0) return fleck[2];
            if (d == 1) return fleck[1];
            if (d == 2 && ArtHash.Chance(px, py, seed + salt + 22 + k, 50)) return fleck[0];
        }
        return null;
    }

    private static void DrawTorch(Canvas img, int ox, int oy, Rgba[] handle, Rgba[] flame)
    {
        for (int py = 6; py < T; py++)
        {
            img.Set(ox + 7, oy + py, py == T - 1 ? handle[0] : handle[1]);
            img.Set(ox + 8, oy + py, handle[0]);
        }
        for (int py = 2; py <= 5; py++)
        for (int px = 6; px <= 9; px++)
        {
            bool outer = px == 6 || px == 9 || py == 5;
            bool core = (px == 7 || px == 8) && py <= 3;
            img.Set(ox + px, oy + py, core ? flame[2] : outer ? flame[0] : flame[1]);
        }
        img.Set(ox + 7, oy + 1, flame[1]);
        img.Set(ox + 8, oy + 1, flame[0]);
    }

    private static void DrawWall(Canvas img, int ox, int oy, Rgba[] pal, int mask, int v, int seed, int salt)
    {
        bool n = (mask & AutoTile.North) == 0, e = (mask & AutoTile.East) == 0;
        bool s = (mask & AutoTile.South) == 0, w = (mask & AutoTile.West) == 0;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            var c = pal[Tone(px, py, v, seed, salt)];
            if ((n && py == 0) || (s && py == T - 1) || (w && px == 0) || (e && px == T - 1)) c = pal[0].Scale(0.8f);
            img.Set(ox + px, oy + py, c);
        }
    }

    /// <summary>Faixa irregular de 2–4 px (em colunas de 2 px) ao longo de um lado; o último pixel é a sombra.</summary>
    private static void DrawTongue(Canvas img, int ox, int oy, Rgba[] pal, int side, int v, int seed, int salt)
    {
        for (int along = 0; along < T; along++)
        {
            int depth = 2 + ArtHash.Range(v * T + along / 2, side, seed + salt + 40, 3);
            for (int d = 0; d < depth; d++)
            {
                var (px, py) = side switch
                {
                    0 => (along, d),
                    1 => (T - 1 - d, along),
                    2 => (along, T - 1 - d),
                    _ => (d, along),
                };
                var c = d == depth - 1 ? pal[0].Scale(0.8f) : pal[Tone(px, py, v, seed, salt)];
                img.Set(ox + px, oy + py, c);
            }
        }
    }
}
```

- [ ] **Step 5: Rodar** — `dotnet test` → verde. Se o teste do cinza (`DirtWithExposedTop_HasAshCap`) falhar por causa de qual tom cai no pixel (8, 2), ajuste o teste para checar que o pixel pertence ao conjunto {tons do `cap`, tons do `cap` × `HighlightFactor`} — sem afrouxar além disso.

- [ ] **Step 6: Commit**

```bash
git add art src/Zyon.ArtGen tests/Zyon.ArtGen.Tests
git commit -m "feat(artgen): blocos contornados, paredes e bordas de mescla" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 5: Jogador, ícones, programa e arte commitada

**Files:**
- Create: `src/Zyon.ArtGen/ItemIconGenerator.cs`, `src/Zyon.ArtGen/PlayerGenerator.cs`, `src/Zyon.ArtGen/ArtBuilder.cs`, `tools/gen-art.sh`
- Modify: `src/Zyon.ArtGen/Program.cs`
- Create (gerados): `data/client/art/*`
- Modify: `tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj` (copiar os PNGs de `data/`)
- Test: `tests/Zyon.ArtGen.Tests/ItemAndPlayerTests.cs`, `tests/Zyon.ArtGen.Tests/ArtUpToDateTests.cs`

**Geometria do jogador** (quadro 32×48, virado para a direita, +X = frente). Ângulo de membro `a` em graus: 0 = para baixo, 90 = para frente, 180 = para cima, negativo = para trás; direção `(sin a, cos a)` com `DeterministicMath`. Ângulo de tela da direção = `90 − a`.

| Parte | Onde |
|---|---|
| Cabeça | x 11–20, y 3–12; olho em (18, 7) |
| Pescoço | x 14–17, y 13 |
| Tronco (roupa) | x 11–20, y 14–29; zíper (cor `detail`) em x 15, y 15–28; cinto y 27 |
| Pernas (roupa) | quadril de trás (13, 30), da frente (18, 30); 4 px de largura, 14 de comprimento; bota de 3 px (`boots`) no fim |
| Braço da frente (`arm`) | ombro (16, 16); 3 px de largura; manga (roupa) t 0–7, mão (pele) t 8–11 |
| Braço de trás | mesmo, ângulo oposto ao da frente ao andar; manga na camada `outfit` (desenhada antes do tronco), mão na `body`; tom escuro |
| Cabelo | x 10–21, y 2–5 e x 10–12, y 6–10 |

Ângulos por quadro: parado — pernas 0, braço 5; andando (k = 1..8, φ = (k−1)/8 · 2π) — perna da frente `30·sin φ`, de trás o oposto, braço da frente `−25·sin φ`; no ar — perna da frente 25, de trás −15, braço 30; golpe (quadros 10–14) — braço 200, 157, 115, 72, 30. Mão = ombro + direção × 11 (arredondado). Cada camada ganha contorno (`Canvas.Outline`, cor `outline`) por quadro.

- [ ] **Step 1: Testes que falham**

`tests/Zyon.ArtGen.Tests/ItemAndPlayerTests.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen.Tests;

public class ItemAndPlayerTests
{
    private static readonly Registry R = RegistryLoader.LoadFromDirectory(Path.Combine(RepoPaths.Root, "data"));
    private static readonly ArtSource Source = ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);

    [Fact]
    public void Icons_OneCellPerItem_BlockItemsCopyTheIsolatedBlock()
    {
        var tiles = TileGenerator.BuildBlocks(Source, R, out var rows);
        var icons = ItemIconGenerator.Build(Source, R, tiles, out var arts);

        Assert.Equal((R.Items.Count - 1) * 16, icons.Width);
        Assert.Equal(16, icons.Height);
        var dirt = arts["dirt"];
        int tx = 0, ty = rows["dirt"] * 16;   // máscara 0, variação 0
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            Assert.Equal(tiles.Get(tx + x, ty + y), icons.Get(dirt.Cell * 16 + x, y));
        Assert.Equal(-90f, dirt.PointDeg);
    }

    [Fact]
    public void Pickaxe_PointsDiagonally_AndHasPixelsAtTheGrip()
    {
        var tiles = TileGenerator.BuildBlocks(Source, R, out _);
        var icons = ItemIconGenerator.Build(Source, R, tiles, out var arts);
        var pick = arts["basic_pickaxe"];

        Assert.Equal(-45f, pick.PointDeg);
        Assert.NotEqual(0, icons.Get(pick.Cell * 16 + pick.GripX, pick.GripY).A);
    }

    [Fact]
    public void Player_StripsHaveTheRightSize()
    {
        var art = PlayerGenerator.Build(Source, out var layers, out var meta);

        Assert.Equal(meta.BodyFrames * meta.FrameWidth, layers["body"].Width);
        Assert.Equal(meta.FrameHeight, layers["body"].Height);
        Assert.Equal(meta.ArmFrames * meta.FrameWidth, layers[ArtManifest.ArmLayer].Width);
        Assert.Equal(["body", "outfit", "hair"], meta.Layers);
        Assert.Equal(meta.ArmFrames, meta.Hand.Count);
        Assert.Equal(meta.ArmFrames, meta.ArmAngleDeg.Count);
        Assert.Same(art, meta);
    }

    [Fact]
    public void Player_FeetTouchTheBottom_AndWalkFramesDiffer()
    {
        PlayerGenerator.Build(Source, out var layers, out var meta);
        var outfit = layers["outfit"];
        int W = meta.FrameWidth;

        bool BottomRowHasPixels(int frame) => Enumerable.Range(0, W).Any(x => outfit.Get(frame * W + x, meta.FrameHeight - 1).A != 0);
        Assert.True(BottomRowHasPixels(meta.Idle));
        bool Same(int f1, int f2) => Enumerable.Range(0, W * meta.FrameHeight)
            .All(i => outfit.Get(f1 * W + i % W, i / W) == outfit.Get(f2 * W + i % W, i / W));
        Assert.False(Same(meta.WalkFirst, meta.WalkFirst + 2));
    }

    [Fact]
    public void Swing_ArmAnglesGoFromUpBackToForwardDown()
    {
        PlayerGenerator.Build(Source, out _, out var meta);

        Assert.Equal(90f - 200f, meta.ArmAngleDeg[meta.SwingFirst]);
        Assert.Equal(90f - 30f, meta.ArmAngleDeg[meta.SwingFirst + meta.SwingCount - 1]);
        var hand = meta.Hand[meta.SwingFirst + meta.SwingCount - 1];
        Assert.True(hand[0] > 16 && hand[1] > 16, "no fim do golpe a mão fica à frente e abaixo do ombro");
    }
}
```

`tests/Zyon.ArtGen.Tests/ArtUpToDateTests.cs`:

```csharp
namespace Zyon.ArtGen.Tests;

/// <summary>Os arquivos em data/client/art/ precisam ser exatamente a saída atual do gerador.</summary>
public class ArtUpToDateTests
{
    [Fact]
    public void CommittedArt_MatchesTheGenerator()
    {
        var generated = ArtBuilder.Build(RepoPaths.Root);
        string dir = Path.Combine(RepoPaths.Root, ArtBuilder.OutputDir);

        foreach (var (file, canvas) in generated.Images)
        {
            string path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), $"{file} não existe — rode tools/gen-art.sh");
            var committed = Png.Decode(File.ReadAllBytes(path));
            Assert.True(committed.Width == canvas.Width && committed.Height == canvas.Height && committed.Pixels.AsSpan().SequenceEqual(canvas.Pixels),
                $"{file} está desatualizado — rode tools/gen-art.sh e commite o resultado");
        }
        string manifest = File.ReadAllText(Path.Combine(dir, Zyon.Client.ArtManifest.FileName)).ReplaceLineEndings("\n");
        Assert.True(manifest == generated.Manifest.ToJson(), "art.json desatualizado — rode tools/gen-art.sh");
    }
}
```

(A comparação é por pixels, não por bytes do PNG: a compressão zlib pode mudar entre versões do .NET sem mudar a imagem.)

Run: `dotnet test tests/Zyon.ArtGen.Tests` → FAIL na compilação.

- [ ] **Step 2: `ItemIconGenerator`**

`src/Zyon.ArtGen/ItemIconGenerator.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

/// <summary>Ícones 16×16 em uma linha (célula = id − 1). Itens de bloco copiam o bloco isolado (máscara 0, variação 0).</summary>
public static class ItemIconGenerator
{
    private const int T = 16;

    public static Canvas Build(ArtSource src, Registry registry, Canvas tiles, out Dictionary<string, ItemArt> arts)
    {
        arts = [];
        var img = new Canvas((registry.Items.Count - 1) * T, T);
        var outline = Rgba.Parse(src.Player.Outline);
        for (int id = 1; id < registry.Items.Count; id++)
        {
            var def = registry.Item((ushort)id);
            int cell = id - 1, ox = cell * T;
            if (src.Items.TryGetValue(def.Name, out var item) && item.Recipe == "pickaxe")
            {
                DrawPickaxe(img, ox, ArtSource.Colors(item.Handle!), ArtSource.Colors(item.Head!));
                img.Outline(ox, 0, T, T, outline);
                arts[def.Name] = new ItemArt { Cell = cell, GripX = 3, GripY = 12, PointDeg = -45f };
            }
            else
            {
                int row = def.PlacesBlockId - 1;   // linha do bloco no tiles.png
                img.Blit(tiles, 0, row * T, T, T, ox, 0);
                arts[def.Name] = new ItemArt { Cell = cell, GripX = 8, GripY = 14, PointDeg = -90f };
            }
        }
        return img;
    }

    /// <summary>Cabo na diagonal (canto inferior esquerdo → centro) e cabeça perpendicular no alto à direita.</summary>
    private static void DrawPickaxe(Canvas img, int ox, Rgba[] handle, Rgba[] head)
    {
        for (int x = 2; x <= 10; x++)
        {
            img.Set(ox + x, 15 - x, handle[1]);
            img.Set(ox + x + 1, 15 - x, handle[0]);
        }
        img.Set(ox + 2, 13, handle[2]);
        for (int x = 7; x <= 14; x++)
        {
            int y = x - 7;
            img.Set(ox + x, y, head[1]);
            img.Set(ox + x - 1, y, head[2]);
            if (y + 1 < T) img.Set(ox + x, y + 1, head[0]);
        }
    }
}
```

- [ ] **Step 3: `PlayerGenerator`**

`src/Zyon.ArtGen/PlayerGenerator.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.WorldGen;

namespace Zyon.ArtGen;

/// <summary>Molde do jogador em camadas. Ver a tabela de geometria do plano 6, Task 5.</summary>
public static class PlayerGenerator
{
    private const int W = 32, H = 48;
    private const int ShoulderX = 16, ShoulderY = 16, ArmLength = 11;

    public static PlayerArt Build(ArtSource src, out Dictionary<string, Canvas> layers, out PlayerArt meta)
    {
        meta = new PlayerArt();
        var p = src.Player;
        var skin = ArtSource.Colors(p.Skin);
        var hair = ArtSource.Colors(p.Hair);
        var outfit = ArtSource.Colors(p.Outfit);
        var boots = ArtSource.Colors(p.Boots);
        var detail = Rgba.Parse(p.Detail);
        var outline = Rgba.Parse(p.Outline);

        var body = new Canvas(meta.BodyFrames * W, H);
        var clothes = new Canvas(meta.BodyFrames * W, H);
        var hairs = new Canvas(meta.BodyFrames * W, H);
        var arm = new Canvas(meta.ArmFrames * W, H);

        for (int f = 0; f < meta.BodyFrames; f++)
        {
            var (front, back, armA) = Pose(meta, f);
            int ox = f * W;
            // braço de trás: manga na roupa (atrás do tronco), mão no corpo
            Limb(clothes, ox, ShoulderX - 2, ShoulderY, -armA, 0, 7, 3, outfit[0]);
            Limb(body, ox, ShoulderX - 2, ShoulderY, -armA, 8, ArmLength, 3, skin[0]);
            // cabeça e pescoço
            Rect(body, ox, 11, 3, 20, 12, skin[1]);
            Rect(body, ox, 11, 3, 11, 12, skin[0]);
            body.Set(ox + 18, 7, outline);
            Rect(body, ox, 14, 13, 17, 13, skin[0]);
            // pernas e botas
            Limb(clothes, ox, 13, 30, back, 0, 13, 4, outfit[0]);
            Limb(clothes, ox, 13, 30, back, 14, 16, 4, boots[0]);
            Limb(clothes, ox, 18, 30, front, 0, 13, 4, outfit[1]);
            Limb(clothes, ox, 18, 30, front, 14, 16, 4, boots[1]);
            // tronco
            Rect(clothes, ox, 11, 14, 20, 29, outfit[1]);
            Rect(clothes, ox, 19, 14, 20, 29, outfit[0]);
            Rect(clothes, ox, 11, 14, 12, 15, outfit[2]);
            Rect(clothes, ox, 15, 15, 15, 28, detail);
            Rect(clothes, ox, 11, 27, 20, 27, outfit[0]);
            // cabelo
            Rect(hairs, ox, 10, 2, 21, 5, hair[1]);
            Rect(hairs, ox, 10, 2, 21, 2, hair[2]);
            Rect(hairs, ox, 10, 6, 12, 10, hair[0]);

            body.Outline(ox, 0, W, H, outline);
            clothes.Outline(ox, 0, W, H, outline);
            hairs.Outline(ox, 0, W, H, outline);
        }

        for (int f = 0; f < meta.ArmFrames; f++)
        {
            float a = f >= meta.SwingFirst ? SwingAngles[f - meta.SwingFirst] : Pose(meta, f).Arm;
            int ox = f * W;
            Limb(arm, ox, ShoulderX, ShoulderY, a, 0, 7, 3, outfit[2]);
            Limb(arm, ox, ShoulderX, ShoulderY, a, 8, ArmLength, 3, skin[1]);
            arm.Outline(ox, 0, W, H, outline);
            var (dx, dy) = Dir(a);
            meta.Hand.Add([ShoulderX + Round(dx * ArmLength), ShoulderY + Round(dy * ArmLength)]);
            meta.ArmAngleDeg.Add(90f - a);
        }

        layers = new Dictionary<string, Canvas>
        {
            ["body"] = body,
            ["outfit"] = clothes,
            ["hair"] = hairs,
            [ArtManifest.ArmLayer] = arm,
        };
        return meta;
    }

    private static readonly float[] SwingAngles = [200f, 157f, 115f, 72f, 30f];

    /// <summary>(perna da frente, perna de trás, braço da frente) em graus para um quadro do corpo.</summary>
    private static (float Front, float Back, float Arm) Pose(PlayerArt meta, int frame)
    {
        if (frame == meta.Air) return (25f, -15f, 30f);
        if (frame >= meta.WalkFirst && frame < meta.WalkFirst + meta.WalkCount)
        {
            float phi = (frame - meta.WalkFirst) / (float)meta.WalkCount * 6.2831853f;
            float s = DeterministicMath.Sin(phi);
            return (30f * s, -30f * s, -25f * s);
        }
        return (0f, 0f, 5f);
    }

    private static (float X, float Y) Dir(float degrees)
    {
        float r = degrees * 0.017453292f;
        return (DeterministicMath.Sin(r), DeterministicMath.Cos(r));
    }

    private static int Round(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>Segmento de membro: do ponto (x0, y0), na direção do ângulo, de t = from até to, com a largura dada.</summary>
    private static void Limb(Canvas c, int ox, int x0, int y0, float degrees, int from, int to, int width, Rgba color)
    {
        var (dx, dy) = Dir(degrees);
        for (int t = from; t <= to; t++)
        {
            int cx = x0 + Round(dx * t), cy = y0 + Round(dy * t);
            for (int k = 0; k < width; k++)
                c.Set(ox + cx - width / 2 + k, cy, color);
        }
    }

    private static void Rect(Canvas c, int ox, int x0, int y0, int x1, int y1, Rgba color)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            c.Set(ox + x, y, color);
    }
}
```

(As pernas terminam em y = 30 + 16 = 46; para tocar a última linha (47), aumente o `to` da bota para 17 se o teste `Player_FeetTouchTheBottom` falhar — o importante é o pé encostar em y 47 no quadro parado.)

- [ ] **Step 4: `ArtBuilder` e `Program`**

`src/Zyon.ArtGen/ArtBuilder.cs`:

```csharp
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

public sealed record GeneratedArt(IReadOnlyDictionary<string, Canvas> Images, ArtManifest Manifest);

/// <summary>Gera toda a arte a partir de art/art.json e data/ (em memória), e grava em data/client/art/.</summary>
public static class ArtBuilder
{
    public const string SourcePath = "art/art.json";
    public const string OutputDir = "data/client/art";

    public static GeneratedArt Build(string repoRoot)
    {
        var registry = RegistryLoader.LoadFromDirectory(Path.Combine(repoRoot, "data"));
        var src = ArtSource.LoadFromFile(Path.Combine(repoRoot, SourcePath), registry);

        var tiles = TileGenerator.BuildBlocks(src, registry, out var tileRows);
        var walls = TileGenerator.BuildWalls(src, registry, out var wallRows);
        var merge = TileGenerator.BuildMerge(src, registry);
        var items = ItemIconGenerator.Build(src, registry, tiles, out var itemArts);
        var player = PlayerGenerator.Build(src, out var layers, out _);

        var manifest = new ArtManifest
        {
            Variations = src.Variations,
            Tiles = tileRows,
            Walls = wallRows,
            MergeOrder = [.. src.MergeOrder],
            Items = itemArts,
            Player = player,
        };
        ArtCatalog.Resolve(manifest, registry);   // a saída precisa passar na mesma validação do jogo

        var images = new Dictionary<string, Canvas>
        {
            [ArtManifest.TilesFile] = tiles,
            [ArtManifest.WallsFile] = walls,
            [ArtManifest.MergeFile] = merge,
            [ArtManifest.ItemsFile] = items,
        };
        foreach (var (layer, canvas) in layers) images[ArtManifest.PlayerFile(layer)] = canvas;
        return new GeneratedArt(images, manifest);
    }

    public static void Write(string repoRoot, GeneratedArt art)
    {
        string dir = Path.Combine(repoRoot, OutputDir);
        foreach (var (file, canvas) in art.Images)
        {
            string path = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Png.Encode(canvas));
        }
        File.WriteAllText(Path.Combine(dir, ArtManifest.FileName), art.Manifest.ToJson());
    }
}
```

`src/Zyon.ArtGen/Program.cs`:

```csharp
using Zyon.ArtGen;
using Zyon.Core.Data;

string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
try
{
    var art = ArtBuilder.Build(root);
    ArtBuilder.Write(root, art);
    Console.WriteLine($"Arte gerada em {Path.Combine(root, ArtBuilder.OutputDir)} ({art.Images.Count} imagens)");
    return 0;
}
catch (DataException ex)
{
    Console.Error.WriteLine($"erro: {ex.Message}");
    return 1;
}
```

`tools/gen-art.sh` (com `chmod +x` e `git add --chmod=+x`):

```bash
#!/usr/bin/env bash
# Gera data/client/art/ a partir de art/art.json e data/. Rode depois de mudar a arte ou adicionar conteúdo.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project src/Zyon.ArtGen -c Release -- "$(pwd)"
```

- [ ] **Step 5: Gerar e commitar a arte**

Run: `tools/gen-art.sh` → "Arte gerada em … (8 imagens)". Depois `dotnet test` → verde (inclui `ArtUpToDateTests`).

Em `tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj`, ao lado da linha que copia `data\**\*.json`, adicionar:

```xml
    <None Include="..\..\data\**\*.png" LinkBase="data" CopyToOutputDirectory="PreserveNewest" />
```

e um teste em `ArtCatalogTests` que carrega a arte real:

```csharp
    [Fact]
    public void RealArt_LoadsAndCoversAllContent()
    {
        var c = ArtCatalog.LoadFromDirectory(TestData.DataDir, TestData.Registry());

        Assert.Equal(TestData.Registry().Blocks.Count, c.TileRowByBlockId.Count);
        Assert.True(File.Exists(c.PathOf(ArtManifest.TilesFile)));
    }
```

(Use o nome real do helper de pasta de dados do `TestData` do Client.)

- [ ] **Step 6: Olhar a arte**

Abra os PNGs gerados (`data/client/art/tiles.png`, `merge.png`, `items.png`, `player/*.png`) num visualizador ou com a ferramenta de leitura de imagens e confira: blocos contornados com cantos arredondados, tocha como palito com chama, cabeça/cabelo/macacão/botas reconhecíveis, braço girando nos quadros 10–14. Ajuste o desenho se algo estiver claramente errado (sem mudar formatos), gere de novo e reporte o que mudou.

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.ArtGen tests tools/gen-art.sh data/client/art
git commit -m "feat(artgen): jogador em camadas, ícones e arte gerada" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 6: Malhas com a arte nova e mescla (Client)

**Files:**
- Create: `src/Zyon.Client/MergeAtlasLayout.cs`
- Modify: `src/Zyon.Client/ChunkMeshBuilder.cs`, `src/Zyon.Client/ClientVisuals.cs`, `data/client/visuals.json`
- Delete: `src/Zyon.Client/PlaceholderAtlas.cs`, `tests/Zyon.Client.Tests/PlaceholderAtlasTests.cs`
- Test: `tests/Zyon.Client.Tests/ChunkMeshBuilderTests.cs`, `tests/Zyon.Client.Tests/ClientVisualsTests.cs`

A linha do atlas deixa de ser o id: vem de `ArtCatalog.TileRowByBlockId` / `WallRowByWallId`.

- [ ] **Step 1: Testes que falham**

Em `ChunkMeshBuilderTests.cs`, troque as chamadas de `Build` para a nova assinatura (`rows` = `catalog.TileRowByBlockId` ou `WallRowByWallId`) e use um catálogo real (`ArtCatalog.LoadFromDirectory(TestData.DataDir, r)`) com `new TileAtlasLayout(rows linhas, variations)`. Acrescente:

```csharp
    private static readonly Registry R = TestData.Registry();
    private static readonly ArtCatalog Art = ArtCatalog.LoadFromDirectory(TestData.DataDir, R);
    private static readonly MergeAtlasLayout Merge = new(Art.Manifest.MergeOrder.Count, Art.Manifest.Variations);

    [Fact]
    public void Merge_SofterNeighbour_AddsOneQuadOnThatSide()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(5, 5, R.BlockId("stone"));
        w.SetBlock(5, 4, R.BlockId("dirt"));      // terra (mais mole) ao norte da rocha
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.BuildMerge(view, 0, 0, Merge, Art.MergeRankByBlockId, data);

        Assert.Equal(1, data.QuadCount);
        Assert.Equal(new System.Numerics.Vector2(5 * 16, 5 * 16), data.Vertices[0]);
        var uv = Merge.Uv(Art.Manifest.MergeOrder.IndexOf("dirt"), side: 0, AutoTile.Variation(5, 5, Merge.Variations));
        Assert.Equal(uv.U0, data.Uvs[0].X, 5);
        Assert.Equal(uv.V0, data.Uvs[0].Y, 5);
    }

    [Fact]
    public void Merge_NeverWithAirNonSolidHarderOrSameMaterial()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(5, 5, R.BlockId("dirt"));
        w.SetBlock(6, 5, R.BlockId("stone"));     // mais duro: a borda vai no tile da rocha, não no da terra
        w.SetBlock(4, 5, R.BlockId("dirt"));      // mesmo material
        w.SetBlock(5, 4, R.BlockId("torch"));     // não sólido
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.BuildMerge(view, 0, 0, Merge, Art.MergeRankByBlockId, data);

        Assert.Equal(1, data.QuadCount);                                      // só a borda da terra sobre a rocha (lado O da rocha)
        Assert.Equal(new System.Numerics.Vector2(6 * 16, 5 * 16), data.Vertices[0]);
    }

    [Fact]
    public void Blocks_UseTheArtRow_NotTheId()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(1, 1, R.BlockId("stone"));
        using var view = TestData.View(w);
        var data = new ChunkMeshData();
        var layout = new TileAtlasLayout(R.Blocks.Count - 1, Art.Manifest.Variations);

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, layout, Art.TileRowByBlockId, data);

        var uv = layout.Uv(Art.TileRowByBlockId[R.BlockId("stone")], 0, AutoTile.Variation(1, 1, layout.Variations));
        Assert.Equal(uv.V0, data.Uvs[0].Y, 5);
    }
```

Em `ClientVisualsTests.cs`, reduza os testes ao que sobra: `sky` lido e validado; JSON inválido; `sky` ausente → erro. Os testes de cores de blocos, paredes e itens saem.

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/MergeAtlasLayout.cs`:

```csharp
namespace Zyon.Client;

/// <summary>merge.png: linha = posição na mergeOrder; coluna = lado × Variations + variação (lado 0 N, 1 L, 2 S, 3 O).</summary>
public sealed class MergeAtlasLayout(int rows, int variations)
{
    public const int Sides = 4;
    private const float InsetPx = 0.01f;

    public int Rows { get; } = Math.Max(1, rows);
    public int Variations { get; } = variations;
    public int WidthPx => Sides * Variations * TileAtlasLayout.TileSize;
    public int HeightPx => Rows * TileAtlasLayout.TileSize;

    public (float U0, float V0, float U1, float V1) Uv(int row, int side, int variation)
    {
        const int s = TileAtlasLayout.TileSize;
        float x = (side * Variations + variation) * s, y = row * s;
        return ((x + InsetPx) / WidthPx, (y + InsetPx) / HeightPx, (x + s - InsetPx) / WidthPx, (y + s - InsetPx) / HeightPx);
    }
}
```

`ChunkMeshBuilder.cs` — nova assinatura de `Build` e o `BuildMerge`, com um `AddQuad` comum:

```csharp
public static class ChunkMeshBuilder
{
    private static readonly (int Dx, int Dy)[] SideOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];   // N, L, S, O

    /// <summary>Um quad por tile não vazio da camada (linha do atlas = rows[id]). Paredes atrás de blocos sólidos são puladas.</summary>
    public static void Build(IWorldView view, int chunkX, int chunkY, TileLayer layer, TileAtlasLayout atlas, IReadOnlyList<int> rows, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            ushort id = layer == TileLayer.Blocks ? view.GetBlock(x, y) : view.GetWall(x, y);
            if (id == 0) continue;
            if (layer == TileLayer.Walls && view.Registry.Block(view.GetBlock(x, y)).Solid) continue;
            var uv = atlas.Uv(rows[id], AutoTile.Mask(view, x, y, layer), AutoTile.Variation(x, y, atlas.Variations));
            AddQuad(output, lx, ly, uv);
        }
    }

    /// <summary>
    /// Bordas de mescla: para cada bloco sólido com rank R e cada vizinho ortogonal sólido com rank menor (mais mole),
    /// um quad com a borda do vizinho naquele lado, por cima do tile.
    /// </summary>
    public static void BuildMerge(IWorldView view, int chunkX, int chunkY, MergeAtlasLayout atlas, IReadOnlyList<int> rankById, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            int rank = rankById[view.GetBlock(x, y)];
            if (rank <= 0) continue;   // não mescla, ou é o mais mole de todos
            for (int side = 0; side < MergeAtlasLayout.Sides; side++)
            {
                int nx = x + SideOffsets[side].Dx, ny = y + SideOffsets[side].Dy;
                if ((uint)nx >= (uint)view.Width || (uint)ny >= (uint)view.Height) continue;
                int neighbour = rankById[view.GetBlock(nx, ny)];
                if (neighbour < 0 || neighbour >= rank) continue;
                AddQuad(output, lx, ly, atlas.Uv(neighbour, side, AutoTile.Variation(x, y, atlas.Variations)));
            }
        }
    }

    private static void AddQuad(ChunkMeshData output, int lx, int ly, (float U0, float V0, float U1, float V1) uv)
    {
        const float s = RenderMath.PixelsPerTile;
        float px = lx * s, py = ly * s;
        int b = output.Vertices.Count;
        output.Vertices.Add(new Vector2(px, py));
        output.Vertices.Add(new Vector2(px + s, py));
        output.Vertices.Add(new Vector2(px + s, py + s));
        output.Vertices.Add(new Vector2(px, py + s));
        output.Uvs.Add(new Vector2(uv.U0, uv.V0));
        output.Uvs.Add(new Vector2(uv.U1, uv.V0));
        output.Uvs.Add(new Vector2(uv.U1, uv.V1));
        output.Uvs.Add(new Vector2(uv.U0, uv.V1));
        output.Indices.Add(b);
        output.Indices.Add(b + 1);
        output.Indices.Add(b + 2);
        output.Indices.Add(b);
        output.Indices.Add(b + 2);
        output.Indices.Add(b + 3);
    }
}
```

`ClientVisuals.cs`: fica só `Sky` (o DTO e a classe perdem blocos, paredes, itens e jogador; `Load(json)` não precisa mais do `Registry` — mantenha o parâmetro só se simplificar os chamadores; reporte). Atualize o resumo da classe ("Cores do cliente que não são arte: o céu").

`data/client/visuals.json`:

```json
{
  "sky": "#7f9fbf"
}
```

Remova `PlaceholderAtlas.cs` e `PlaceholderAtlasTests.cs`.

**Atenção:** o `Zyon.Game` vai parar de compilar (usa `PlaceholderAtlas`, `ItemColor`, `Player`, a assinatura antiga de `Build`). Isso é esperado e a Task 8 conserta; nesta tarefa rode só `dotnet test` (os projetos de teste não dependem do Game).

- [ ] **Step 3: Rodar** — `dotnet test` → verde.

- [ ] **Step 4: Commit**

```bash
git add -A src/Zyon.Client tests/Zyon.Client.Tests data/client/visuals.json
git commit -m "feat(client): malhas usam o atlas de arte e bordas de mescla" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 7: Animação do jogador (Client)

**Files:**
- Create: `src/Zyon.Client/PlayerAnimator.cs`
- Test: `tests/Zyon.Client.Tests/PlayerAnimatorTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Client.Tests/PlayerAnimatorTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Items;

namespace Zyon.Client.Tests;

public class PlayerAnimatorTests
{
    private static readonly Zyon.Core.Data.Registry R = TestData.Registry();
    private static readonly ArtCatalog Art = ArtCatalog.LoadFromDirectory(TestData.DataDir, R);
    private static readonly PlayerArt P = Art.Manifest.Player;
    private static ushort Pickaxe => R.ItemId("basic_pickaxe");
    private static ushort Torch => R.ItemId("torch");

    private static EntityView Player(float vx = 0, bool onGround = true, int facing = 1, bool using_ = false, int aimX = 0, ushort held = 0) =>
        new(1, EntityKind.Player, new Vector2(10.5f, 20f), new Vector2(10.5f, 20f), new Vector2(1.25f, 2.75f), ItemStack.Empty,
            new Vector2(vx, 0), onGround, facing, using_, aimX, 20, held);

    [Fact]
    public void Standing_IsIdle_NoItemShownForTools()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();

        var pose = anim.Update(Player(held: Pickaxe), 1 / 60f, ref s);

        Assert.Equal(P.Idle, pose.BodyFrame);
        Assert.Equal(P.Idle, pose.ArmFrame);
        Assert.Equal((ushort)0, pose.ItemId);
        Assert.False(pose.FacingLeft);
    }

    [Fact]
    public void Walking_CyclesThroughWalkFrames_FasterWhenFaster()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        var frames = new HashSet<int>();
        for (int i = 0; i < 60; i++) frames.Add(anim.Update(Player(vx: 9f), 1 / 60f, ref s).BodyFrame);

        Assert.All(frames, f => Assert.InRange(f, P.WalkFirst, P.WalkFirst + P.WalkCount - 1));
        Assert.Equal(P.WalkCount, frames.Count);

        var slow = new PlayerAnimState();
        var fast = new PlayerAnimState();
        anim.Update(Player(vx: 2f), 0.1f, ref slow);
        anim.Update(Player(vx: 8f), 0.1f, ref fast);
        Assert.True(fast.WalkPhase > slow.WalkPhase);
    }

    [Fact]
    public void InAir_UsesAirFrame()
    {
        var s = new PlayerAnimState();
        Assert.Equal(P.Air, new PlayerAnimator(Art, R).Update(Player(vx: 5f, onGround: false), 1 / 60f, ref s).BodyFrame);
    }

    [Fact]
    public void Using_SwingsTheArm_ShowsTheTool_AndFacesTheAim()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        var arms = new List<int>();
        PlayerPose pose = default;
        float period = PlayerAnimator.SwingSeconds / R.Item(Pickaxe).ToolSpeed;
        int steps = (int)MathF.Round(period * 60);
        for (int i = 0; i < steps; i++)
        {
            pose = anim.Update(Player(facing: 1, using_: true, aimX: 5, held: Pickaxe), 1 / 60f, ref s);
            arms.Add(pose.ArmFrame);
        }

        Assert.True(pose.FacingLeft);                     // mira à esquerda do jogador (x 5 < 10,5)
        Assert.Equal(Pickaxe, pose.ItemId);
        Assert.Equal(P.SwingFirst, arms[0]);
        Assert.Contains(P.SwingFirst + P.SwingCount - 1, arms);
        Assert.True(arms.Zip(arms.Skip(1)).All(p => p.Second >= p.First), "o arco avança numa volta");
        var item = Art.ItemByItemId[Pickaxe]!;
        Assert.Equal(P.ArmAngleDeg[pose.ArmFrame] - item.PointDeg, pose.ItemRotationDeg, 3);
        Assert.Equal(P.Hand[pose.ArmFrame][0], pose.HandX);
    }

    [Fact]
    public void HoldingABlockItem_ShowsItUpright_WithoutUsing()
    {
        var s = new PlayerAnimState();

        var pose = new PlayerAnimator(Art, R).Update(Player(held: Torch), 1 / 60f, ref s);

        Assert.Equal(Torch, pose.ItemId);
        Assert.Equal(0f, pose.ItemRotationDeg, 3);
    }

    [Fact]
    public void NotUsing_FacesTheMovementSide()
    {
        var s = new PlayerAnimState();
        Assert.True(new PlayerAnimator(Art, R).Update(Player(vx: -3f, facing: -1), 1 / 60f, ref s).FacingLeft);
    }

    [Fact]
    public void UsingWithEmptyHand_DoesNotSwing()
    {
        var s = new PlayerAnimState();
        var pose = new PlayerAnimator(Art, R).Update(Player(using_: true, aimX: 5), 1 / 60f, ref s);

        Assert.Equal(P.Idle, pose.ArmFrame);
        Assert.Equal((ushort)0, pose.ItemId);
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/PlayerAnimator.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Entities;

namespace Zyon.Client;

/// <summary>Estado de animação de um jogador entre frames (um por jogador na tela).</summary>
public struct PlayerAnimState
{
    public float WalkPhase;
    public float SwingPhase;
    public bool WasSwinging;
}

/// <summary>
/// Pose de um frame. Coordenadas no quadro do jogador virado para a direita; FacingLeft espelha tudo.
/// ItemRotationDeg é o ângulo de tela do ícone com o ponto de encaixe na mão (HandX, HandY).
/// </summary>
public readonly record struct PlayerPose(int BodyFrame, int ArmFrame, bool FacingLeft, ushort ItemId, float ItemRotationDeg, int HandX, int HandY);

/// <summary>
/// Escolhe os quadros do jogador: parado, andando (cadência proporcional à velocidade), no ar e golpe (braço em arco
/// enquanto o botão de uso está pressionado com um item na mão). Ao usar, olha para a mira; senão, para o lado do Core.
/// </summary>
public sealed class PlayerAnimator(ArtCatalog art, Registry registry)
{
    /// <summary>Duração de uma volta do golpe com velocidade de ferramenta 1.</summary>
    public const float SwingSeconds = 0.35f;

    /// <summary>Quadros de caminhada por tile percorrido.</summary>
    public const float WalkFramesPerTile = 4f;

    private const float WalkThreshold = 0.1f;

    public PlayerPose Update(in EntityView e, float dt, ref PlayerAnimState s)
    {
        var p = art.Manifest.Player;
        var held = e.HeldItemId == 0 ? null : registry.Item(e.HeldItemId);
        bool swinging = e.UsingItem && held is not null;
        bool facingLeft = swinging ? e.AimX + 0.5f < e.Position.X : e.Facing < 0;

        int body;
        if (!e.OnGround)
        {
            body = p.Air;
            s.WalkPhase = 0;
        }
        else if (MathF.Abs(e.Velocity.X) > WalkThreshold)
        {
            s.WalkPhase = (s.WalkPhase + MathF.Abs(e.Velocity.X) * dt * WalkFramesPerTile) % p.WalkCount;
            body = p.WalkFirst + (int)s.WalkPhase;
        }
        else
        {
            body = p.Idle;
            s.WalkPhase = 0;
        }

        int arm = body;
        if (swinging)
        {
            float speed = held!.ToolSpeed > 0 ? held.ToolSpeed : 1f;
            if (!s.WasSwinging) s.SwingPhase = 0;
            else s.SwingPhase = (s.SwingPhase + dt * speed / SwingSeconds) % 1f;
            arm = p.SwingFirst + Math.Min(p.SwingCount - 1, (int)(s.SwingPhase * p.SwingCount));
        }
        s.WasSwinging = swinging;

        ushort shown = 0;
        float rotation = 0;
        if (held is not null && (swinging || held.PlacesBlockId != 0))
        {
            shown = held.Id;
            var itemArt = art.ItemByItemId[held.Id]!;
            rotation = swinging ? p.ArmAngleDeg[arm] - itemArt.PointDeg : -90f - itemArt.PointDeg;
        }
        var hand = p.Hand[arm];
        return new PlayerPose(body, arm, facingLeft, shown, rotation, hand[0], hand[1]);
    }
}
```

(O primeiro frame do golpe começa na fase 0 — o arco sai sempre de cima-atrás. Se o teste de monotonia pegar a virada da volta por arredondamento, ajuste `steps` para `period * 60 − 1`, não a regra.)

- [ ] **Step 3: Rodar** — `dotnet test` → verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client/PlayerAnimator.cs tests/Zyon.Client.Tests/PlayerAnimatorTests.cs
git commit -m "feat(client): animação do jogador com golpe e item na mão" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 8: Arte no jogo (Godot)

**Files:**
- Create: `src/Zyon.Game/Rendering/ArtTextures.cs`, `src/Zyon.Game/Rendering/PlayerNode.cs`, `src/Zyon.Game/Rendering/ItemIcons.cs`
- Modify: `src/Zyon.Game/GameFlow.cs`, `src/Zyon.Game/GameRoot.cs`, `src/Zyon.Game/Rendering/WorldRenderer.cs`, `src/Zyon.Game/Rendering/ChunkNode.cs`, `src/Zyon.Game/Rendering/EntityRenderer.cs`, `src/Zyon.Game/Hud/Hotbar.cs`, `src/Zyon.Game/Hud/InventorySlot.cs`, `src/Zyon.Game/Hud/InventoryPanel.cs` (se usar cores de item), `src/Zyon.Game/SmokeTest.cs`

Leia os arquivos antes; os trechos abaixo mostram o essencial e precisam ser encaixados no código real. Só API do Godot 4.

- [ ] **Step 1: Carregar a arte**

`src/Zyon.Game/Rendering/ArtTextures.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>Texturas da arte (PNGs de data/client/art), carregadas uma vez.</summary>
public sealed class ArtTextures
{
    public required Texture2D Tiles { get; init; }
    public required Texture2D Walls { get; init; }
    public required Texture2D Merge { get; init; }
    public required Texture2D Items { get; init; }
    public required IReadOnlyList<Texture2D> PlayerLayers { get; init; }   // na ordem de PlayerArt.Layers
    public required Texture2D PlayerArm { get; init; }

    public static ArtTextures Load(ArtCatalog art)
    {
        var layers = new List<Texture2D>();
        foreach (var layer in art.Manifest.Player.Layers) layers.Add(Png(art, ArtManifest.PlayerFile(layer)));
        return new ArtTextures
        {
            Tiles = Png(art, ArtManifest.TilesFile),
            Walls = Png(art, ArtManifest.WallsFile),
            Merge = Png(art, ArtManifest.MergeFile),
            Items = Png(art, ArtManifest.ItemsFile),
            PlayerLayers = layers,
            PlayerArm = Png(art, ArtManifest.PlayerFile(ArtManifest.ArmLayer)),
        };
    }

    private static ImageTexture Png(ArtCatalog art, string file)
    {
        string path = art.PathOf(file);
        var image = new Image();
        var error = image.LoadPngFromBuffer(System.IO.File.ReadAllBytes(path));
        if (error != Error.Ok) throw new Zyon.Core.Data.DataException($"arte: não consegui ler '{path}' ({error})");
        return ImageTexture.CreateFromImage(image);
    }
}
```

Em `GameFlow.EnsureInitialized`, depois de `Visuals`: `Art = ArtCatalog.LoadFromDirectory(DataDir, Registry); ArtTextures = Rendering.ArtTextures.Load(Art);` com as propriedades estáticas `ArtCatalog Art` e `ArtTextures ArtTextures`. A chamada a `ClientVisuals.LoadFromDirectory` passa a usar a assinatura nova da Task 6.

- [ ] **Step 2: Mundo — atlas de arte e malha de mescla**

`ChunkNode`: terceira `MeshInstance2D` (`merge`) depois de `blocks`, com sua `LayerMesh`; `Init(Texture2D wallAtlas, Texture2D blockAtlas, Texture2D mergeAtlas)`; `SetMerge(ChunkMeshData)`. Remova o `WallTint` (as paredes já vêm escurecidas da paleta) — ou mantenha em 1, reporte.

`WorldRenderer`:
- `Init(IWorldView worldView, ArtCatalog art, ArtTextures textures)`;
- `blockLayout = new TileAtlasLayout(view.Registry.Blocks.Count - 1, art.Manifest.Variations)`, `wallLayout = new TileAtlasLayout(view.Registry.Walls.Count - 1, art.Manifest.Variations)`, `mergeLayout = new MergeAtlasLayout(art.Manifest.MergeOrder.Count, art.Manifest.Variations)`;
- onde monta um chunk: `ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Walls, wallLayout, art.WallRowByWallId, scratch); node.SetWalls(scratch);` — idem blocos com `TileRowByBlockId` — e `ChunkMeshBuilder.BuildMerge(view, cx, cy, mergeLayout, art.MergeRankByBlockId, scratch); node.SetMerge(scratch);`;
- **mescla e chunks vizinhos:** a borda depende do vizinho, então um `TileChanged` na borda de um chunk precisa remontar também o chunk vizinho. Confira se o `DirtyChunkTracker.MarkTile` já marca os vizinhos quando o tile está na borda (o autotile já precisava disso); se não marcar, corrija lá com teste no Client;
- remova `BuildAtlas` e o uso de `PlaceholderAtlas`.

- [ ] **Step 3: Ícones de itens**

`src/Zyon.Game/Rendering/ItemIcons.cs`:

```csharp
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>AtlasTexture por item (16×16 dentro de items.png), criadas uma vez.</summary>
public sealed class ItemIcons
{
    private readonly AtlasTexture?[] icons;

    public ItemIcons(ArtCatalog art, Texture2D items)
    {
        icons = new AtlasTexture?[art.ItemByItemId.Count];
        for (int id = 1; id < icons.Length; id++)
        {
            var a = art.ItemByItemId[id]!;
            icons[id] = new AtlasTexture { Atlas = items, Region = new Rect2(a.Cell * 16, 0, 16, 16) };
        }
    }

    public Texture2D? Get(ushort itemId) => itemId < icons.Length ? icons[itemId] : null;
}
```

`Hotbar` e `InventorySlot`: trocar o `ColorRect` do ícone por `TextureRect` (`ExpandMode = IgnoreSize`, `StretchMode = KeepAspectCentered`, `TextureFilter = Nearest`, mesmo tamanho e `MouseFilter.Ignore`); `Texture = icons.Get(stack.ItemId)` (ou `null` se vazio). Recebem `ItemIcons` no `Init` no lugar de `ClientVisuals`. A pré-visualização de arrastar também vira `TextureRect` com o ícone.

- [ ] **Step 4: Itens no chão com ícones**

No `EntityRenderer`, o `MultiMesh` dos itens passa a ter textura:
- malha: `ArrayMesh` de um quad 12×12 centrado, com UVs (0,0)–(1,1) e Y para baixo (não use `QuadMesh`: em 2D ele sai espelhado);
- `MultiMesh.UseCustomData = true`, `UseColors = false`;
- `MultiMeshInstance2D.Texture = textures.Items`, `TextureFilter = Nearest`, material com o shader:

```glsl
shader_type canvas_item;
uniform float cells;
void vertex() {
    UV = vec2((UV.x + INSTANCE_CUSTOM.x) / cells, UV.y);
}
```

  com `cells = art.ItemByItemId.Count - 1`;
- por instância: `SetInstanceCustomData(i, new Color(cell, 0, 0, 0))` com `cell = art.ItemByItemId[itemId].Cell`.

- [ ] **Step 5: Jogador em camadas**

`src/Zyon.Game/Rendering/PlayerNode.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Um jogador: camadas (corpo, roupa, cabelo), item na mão e braço da frente. A origem do nó fica nos pés;
/// virar para a esquerda é Scale.X = −1 no nó "pivot", que espelha tudo junto.
/// </summary>
public partial class PlayerNode : Node2D
{
    private readonly Node2D pivot = new();
    private readonly List<Sprite2D> layers = new();
    private readonly Sprite2D item = new();
    private readonly Sprite2D arm = new();
    private PlayerArt art = null!;
    private ArtCatalog catalog = null!;

    public PlayerAnimState State;

    public void Init(ArtCatalog artCatalog, ArtTextures textures)
    {
        catalog = artCatalog;
        art = artCatalog.Manifest.Player;
        AddChild(pivot);
        foreach (var texture in textures.PlayerLayers)
        {
            var s = Layer(texture, art.BodyFrames);
            layers.Add(s);
            pivot.AddChild(s);
        }
        item.Texture = textures.Items;
        item.RegionEnabled = true;
        item.Centered = false;
        item.TextureFilter = TextureFilterEnum.Nearest;
        pivot.AddChild(item);
        arm.Texture = textures.PlayerArm;
        ConfigureFrames(arm, art.ArmFrames);
        pivot.AddChild(arm);
    }

    public void Apply(PlayerPose pose)
    {
        pivot.Scale = new Vector2(pose.FacingLeft ? -1 : 1, 1);
        foreach (var s in layers) s.Frame = pose.BodyFrame;
        arm.Frame = pose.ArmFrame;
        item.Visible = pose.ItemId != 0;
        if (pose.ItemId == 0) return;
        var a = catalog.ItemByItemId[pose.ItemId]!;
        item.RegionRect = new Rect2(a.Cell * 16, 0, 16, 16);
        item.Offset = new Vector2(-a.GripX, -a.GripY);
        item.Position = new Vector2(pose.HandX - art.FeetX, pose.HandY - art.FeetY);
        item.RotationDegrees = pose.ItemRotationDeg;
    }

    private Sprite2D Layer(Texture2D texture, int frames)
    {
        var s = new Sprite2D { Texture = texture };
        ConfigureFrames(s, frames);
        return s;
    }

    private void ConfigureFrames(Sprite2D s, int frames)
    {
        s.Hframes = frames;
        s.Centered = false;
        s.Offset = new Vector2(-art.FeetX, -art.FeetY);
        s.TextureFilter = TextureFilterEnum.Nearest;
    }
}
```

No `EntityRenderer`:
- `Init(IWorldView, ArtCatalog, ArtTextures)`; um `PlayerAnimator` único; `Dictionary<int, PlayerNode>` no lugar dos `ColorRect`;
- `Refresh(float alpha, float dt)`: para cada jogador, `node.Position` = pés em pixels = `ToPixels(interpolado) + (0, tamanho.Y × 16 / 2)` (centro do AABB + meia altura; arredonde para pixel inteiro, como a câmera); `var pose = animator.Update(e, dt, ref node.State); node.Apply(pose);`;
- expor, para o smoke test, do jogador local: `int LocalBodyFramesSeen` (quantidade de `BodyFrame` diferentes vistos) e `bool LocalSwingSeen` (algum `ArmFrame >= SwingFirst`). Guarde um `HashSet<int>` só para o id local (recebido no `Init`).

No `GameRoot`: passar `GameFlow.Art`/`GameFlow.ArtTextures` aos renderizadores e criar `ItemIcons` para hotbar e inventário; `entityRenderer.Refresh(alpha, (float)delta)`.

- [ ] **Step 6: Smoke test**

No bloco que imprime `SMOKE OK`, antes de imprimir:

```csharp
                        if (entityRenderer.LocalBodyFramesSeen < 2)
                            throw new System.InvalidOperationException("o jogador não mudou de quadro ao andar");
                        if (!entityRenderer.LocalSwingSeen)
                            throw new System.InvalidOperationException("o jogador não golpeou ao minerar");
```

e acrescente `arte ok` à linha `SMOKE OK`.

- [ ] **Step 7: Verificar**

`dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0 avisos; `dotnet test` → verde; `tools/smoke-test.sh` → `SMOKE OK ... save ok, luz ok, arte ok`.

- [ ] **Step 8: Commit**

```bash
git add src/Zyon.Game src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(game): mundo, itens e jogador animado com a arte gerada" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 9: Captura de tela, documentação e verificação

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Captura para revisão visual**

Gere quadros do jogo com janela (não headless), num mundo descartável, para revisão:

```bash
mkdir -p "$SCRATCH/shots"
"$GODOT" --path src/Zyon.Game --write-movie "$SCRATCH/shots/frame.png" --fixed-fps 60 --quit-after 420 -- --smoke-test --saves-dir "$SCRATCH/saves"
```

(`$SCRATCH` = uma pasta temporária; `$GODOT` como no `CLAUDE.md`. Se `--smoke-test` exigir outros argumentos, use os de `tools/smoke-test.sh` sem `--headless`.) Reporte o caminho de 3 quadros: um com o jogador andando, um minerando (golpe) e um com a tocha colocada.

- [ ] **Step 2: `CLAUDE.md`**

Em "Estrutura", acrescentar:

```markdown
- `src/Zyon.ArtGen` — gerador de arte (sem Godot): lê `art/art.json` (paletas, receitas, `mergeOrder`, cores do jogador) e os dados, e grava `data/client/art/` (PNGs + `art.json`). Rode `tools/gen-art.sh`; os PNGs são commitados e `ArtUpToDateTests` falha se estiverem desatualizados. Testes em `tests/Zyon.ArtGen.Tests`.
```

Em "Regras invioláveis":

```markdown
- Conteúdo novo (bloco, parede, item) precisa de arte: adicione a entrada em `art/art.json` (itens de bloco usam o bloco) e rode `tools/gen-art.sh`; o jogo recusa carregar conteúdo sem arte.
- Formatos da arte (células 16×16, ordem das colunas, tiras do jogador 32×48, ângulos de tela) estão no plano 6 ("Formatos"); trocar um PNG por arte feita à mão exige manter o formato — e tirar aquele arquivo do gerador.
```

E atualizar: a menção ao `PlaceholderAtlas`/"cores e atlas provisórios" no item do `Zyon.Client` (agora "arte (`ArtCatalog`), malhas com mescla, animação do jogador (`PlayerAnimator`)"), a de `visuals.json` no item do `Zyon.Game` (só a cor do céu), "Comandos" com `tools/gen-art.sh`, e o smoke test (`arte ok`: o jogador muda de quadro ao andar e golpeia ao minerar).

- [ ] **Step 3: Verificação final**

`dotnet build -c Release` → 0 avisos; `dotnet test` e `dotnet test -c Release` → verdes; `tools/smoke-test.sh` → `SMOKE OK ... arte ok`; `tools/gen-art.sh` seguido de `git status --short` → nada muda (a arte commitada está em dia).

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: gerador de arte e regras de conteúdo com arte" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

- [ ] **Step 5: Teste manual (pelo usuário)** — critérios do spec (seção 9):
  1. Blocos texturizados e contornados; fronteiras terra/rocha/rocha profunda mescladas.
  2. Jogador de macacão anda, pula e golpeia com a picareta na mão, virando para a mira.
  3. Ícones na hotbar, no inventário, no chão e na mão (tocha selecionada aparece na mão).
  4. FPS sem queda (F3).
