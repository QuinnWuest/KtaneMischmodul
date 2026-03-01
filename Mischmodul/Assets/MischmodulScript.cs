using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class MischmodulScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] buttons;
    public KMSelectable glitchButton;
    public SpriteRenderer[] sprites;
    public SpriteRenderer bgSprite;
    public Sprite[] glitches;
    public Sprite black;
    public Sprite BlankSprite;

    private Sprite chosenIcon;
    private Sprite[] displayedIcons = new Sprite[25];

    int? selected = null;
    int[] solution = Enumerable.Range(0, 25).ToArray();
    int[] grid = Enumerable.Range(0, 25).ToArray();
    string[] coords = new string[] { "A5", "B5", "C5", "D5", "E5", "A4", "B4", "C4", "D4", "E4", "A3", "B3", "C3", "D3", "E3", "A2", "B2", "C2", "D2", "E2", "A1", "B1", "C1", "D1", "E1" };

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    bool glitching;
    bool _readyToStartup;
    private Color[][] displayedPixels = new Color[25][];
    int _chosenModIx;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        glitchButton.OnInteract += delegate () { StartCoroutine(GlitchEffect(0.5f)); return false; };
        GetComponent<KMBombModule>().OnActivate += delegate () { Activate(); };
        Bomb.OnBombExploded += delegate ()
        {
            if (!moduleSolved)
            {
                Debug.LogFormat("[Mischmodul #{0}] Bomb detonation detected. Upon termination, the module displayed the following grid:", moduleId);
                LogLetters(grid);
            }
        };
    }

    void Start()
    {
        IconFetch.Instance.WaitForFetch(OnFetched);
    }

    private Texture2D Texture;

    private void OnFetched(bool error)
    {
        if (error)
        {
            Debug.LogFormat("[Mischmodul #{0}] The module failed to fetch the icons. Using blank sprite instead.", moduleId);
            _chosenModIx = -1;
            chosenIcon = BlankSprite;
        }
        else
        {
            var modsOnBomb = _moduleList.Where(x => Bomb.GetModuleNames().Select(nm => nm.ToUpper())
                    .Contains(x[0].ToUpper())).Select(i => i[0]).ToArray();

            if (UnityEngine.Random.Range(0, 2) == 0 && modsOnBomb.Length != 0)
                _chosenModIx = UnityEngine.Random.Range(0, modsOnBomb.Length);
            else
                _chosenModIx = UnityEngine.Random.Range(0, _moduleList.Length);

            Texture = IconFetch.Instance.GetIcon(_moduleList[_chosenModIx][1]);
            Texture.wrapMode = TextureWrapMode.Clamp;
            Texture.filterMode = FilterMode.Point;
            chosenIcon = Sprite.Create(Texture, new Rect(0.0f, 0.0f, Texture.width, Texture.height), new Vector2(0.5f, 0.5f), 100.0f);
        }

        bgSprite.sprite = chosenIcon;

        GetTiles();
        SetTiles();

        _readyToStartup = true;
    }

    void Activate()
    {
        StartCoroutine(WaitToStartup());
    }

    private IEnumerator WaitToStartup()
    {
        while (!_readyToStartup)
            yield return null;
        foreach (KMSelectable button in buttons)
        {
            button.OnInteract += delegate () { KeyPress(Array.IndexOf(buttons, button)); return false; };
            button.OnHighlight += delegate ()
            {
                if (!moduleSolved && !glitching)
                    sprites[Array.IndexOf(buttons, button)].sprite = black;
            };
            button.OnHighlightEnded += delegate ()
            {
                int ix = Array.IndexOf(buttons, button);
                if (!moduleSolved && !glitching && ix != selected)
                    sprites[ix].sprite = displayedIcons[grid[ix]];
            };
        }
        Audio.PlaySoundAtTransform("Intro", transform);
        grid.Shuffle();
        SetTiles();
        DoLogging();
    }

    void KeyPress(int pos)
    {
        if (moduleSolved) return;
        if (selected == null)
        {
            Audio.PlaySoundAtTransform("PistonOut", buttons[pos].transform);
            selected = pos;
            sprites[pos].sprite = black;
        }
        else if (pos == selected)
        {
            Audio.PlaySoundAtTransform("PistonIn", buttons[pos].transform);
            selected = null;
            sprites[pos].sprite = displayedIcons[grid[pos]];
        }
        else
        {
            Audio.PlaySoundAtTransform("PistonIn", buttons[pos].transform);
            int temp = grid[selected.Value];
            grid[selected.Value] = grid[pos];
            grid[pos] = temp;
            selected = null;
            SetTiles();
            StartCoroutine(CheckSolve());
        }
    }

    void GetTiles()
    {
        for (int i = 0; i < 25; i++)
        {
            displayedIcons[i] = Sprite.Create(chosenIcon.texture, new Rect((6 * (i % 5)) + 1, 6 * (i / 5) + 1, 6, 6), new Vector2(0.5f, 0.5f));
            displayedIcons[i].texture.wrapMode = TextureWrapMode.Clamp;
            displayedPixels[i] = displayedIcons[i].texture.GetPixels((int)displayedIcons[i].textureRect.x, (int)displayedIcons[i].textureRect.y, 6, 6);
        }
    }
    void SetTiles()
    {
        for (int i = 0; i < 25; i++)
            sprites[i].sprite = displayedIcons[grid[i]];
    }

    void DoLogging()
    {
        if (_chosenModIx != -1)
            Debug.LogFormat("[Mischmodul #{0}] The chosen module icon is {1}.", moduleId, _moduleList[_chosenModIx][0]);

        Debug.LogFormat("[Mischmodul #{0}] The generated grid is as follows:", moduleId);
        LogLetters(grid);
        Debug.LogFormat("[Mischmodul #{0}] (To solve the module, alphabetize the above list)", moduleId);
        Debug.LogFormat("[Mischmodul #{0}] If you feel this icon has too high a level of ambiguity, please contact tandyCake#1377 on Discord.", moduleId);
    }

    IEnumerator CheckSolve()
    {
        if (Enumerable.Range(0, 25).All(x => IsCorrect(x)))
        {
            for (int i = 0; i < 25; i++)
                sprites[i].sprite = displayedIcons[i];
            moduleSolved = true;
            yield return new WaitForSeconds(0.1f);
            Audio.PlaySoundAtTransform("Solve", transform);
            yield return new WaitForSeconds(0.5f);
            GetComponent<KMBombModule>().HandlePass();
        }
        yield return null;
    }

    bool IsCorrect(int pos)
    {
        if (grid[pos] == solution[pos])
            return true;
        Color[] thisPixels = displayedPixels[grid[pos]];
        for (int i = 0; i < 36; i++)
            if (!thisPixels[i].Equals(displayedPixels[pos][i]))
                return false;
        return true;
    }


    IEnumerator GlitchEffect(float time)
    {
        if (glitching)
            yield break;
        glitching = true;
        selected = null;
        for (float elapsed = 0; elapsed < time; elapsed += 0.075f)
        {
            for (int i = 0; i < 25; i++)
                if (!IsCorrect(i))
                    sprites[i].sprite = glitches.PickRandom();
            yield return new WaitForSecondsRealtime(0.075f);
        }
        SetTiles();
        glitching = false;
    }

    void LogLetters(int[] input)
    {
        string output = string.Empty;
        for (int i = 0; i < 25; i++)
        {
            output += (char)(input[i] + 'A');
            if (i % 5 == 4)
            {
                Debug.LogFormat("[Mischmodul #{0}] {1}", moduleId, output);
                output = string.Empty;
            }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} swap A2 E3 to swap those coordinates. Commands can be chained with spaces. Use <!{0} test> to flicker the squares.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string input)
    {

        string Command = input.Trim().ToUpperInvariant();
        List<string> parameters = Command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parameters[0] == "SWAP")
            parameters.RemoveAt(0);
        if (parameters.All(x => coords.Contains(x)))
        {
            if ((parameters.Count % 2 == 0) ^ selected == null)
            {
                yield return "sendtochaterror All swaps need to be concluded.";
                yield break;
            }
            yield return null;
            foreach (string coord in parameters)
            {
                buttons[Array.IndexOf(coords, coord)].OnInteract();
                yield return new WaitForSeconds(0.2f);
            }
        }
        else if (Regex.IsMatch(Command, @"^\s*(test)|(flash)|(inspect)|(glitch)|(flicker)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            yield return null;
            StartCoroutine(GlitchEffect(1.5f));
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        //while (!moduleSolved)
        do
        {
            if (selected != null)
            {
                buttons[selected.Value].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            for (int i = 0; i < 25; i++)
            {
                if (grid[i] != i)
                {
                    buttons[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    for (int j = i; j < 25; j++)
                    {
                        if (grid[j] == i)
                        {
                            buttons[j].OnInteract();
                            yield return new WaitForSeconds(0.001f);
                        }
                    }
                }
            }
        } while (false);
    }

    private static T[] NewArray<T>(params T[] array) { return array; }

    private static readonly string[][] _moduleList = NewArray
    (
        new string[] { "Capacitor Discharge", "NeedyCapacitor" },
        new string[] { "Complicated Wires", "Venn" },
        new string[] { "Keypad", "Keypad" },
        new string[] { "Knob", "NeedyKnob" },
        new string[] { "Maze", "Maze" },
        new string[] { "Memory", "Memory" },
        new string[] { "Morse Code", "Morse" },
        new string[] { "Password", "Password" },
        new string[] { "Simon Says", "Simon" },
        new string[] { "The Button", "BigButton" },
        new string[] { "Venting Gas", "NeedyVentGas" },
        new string[] { "Who's on First", "WhosOnFirst" },
        new string[] { "Wire Sequence", "WireSequence" },
        new string[] { "Wires", "Wires" },
        new string[] { "Colour Flash", "ColourFlash" },
        new string[] { "Piano Keys", "PianoKeys" },
        new string[] { "Semaphore", "Semaphore" },
        new string[] { "Emoji Math", "Emoji Math" },
        new string[] { "Math", "Needy Math" },
        new string[] { "Two Bits", "TwoBits" },
        new string[] { "Anagrams", "AnagramsModule" },
        new string[] { "Word Scramble", "WordScrambleModule" },
        new string[] { "Combination Lock", "combinationLock" },
        new string[] { "Motion Sense", "MotionSense" },
        new string[] { "Listening", "Listening" },
        new string[] { "Round Keypad", "KeypadV2" },
        new string[] { "Connection Check", "graphModule" },
        new string[] { "Morsematics", "MorseV2" },
        new string[] { "Orientation Cube", "OrientationCube" },
        new string[] { "Forget Me Not", "MemoryV2" },
        new string[] { "Letter Keys", "LetterKeys" },
        new string[] { "Astrology", "spwizAstrology" },
        new string[] { "Mystic Square", "MysticSquareModule" },
        new string[] { "Turn The Key", "TurnTheKey" },
        new string[] { "Cruel Piano Keys", "CruelPianoKeys" },
        new string[] { "Tetris", "spwizTetris" },
        new string[] { "Turn The Keys", "TurnTheKeyAdvanced" },
        new string[] { "3D Maze", "spwiz3DMaze" },
        new string[] { "Mouse In The Maze", "MouseInTheMaze" },
        new string[] { "Silly Slots", "SillySlots" },
        new string[] { "Number Pad", "NumberPad" },
        new string[] { "Simon States", "SimonV2" },
        new string[] { "Laundry", "Laundry" },
        new string[] { "Alphabet", "alphabet" },
        new string[] { "Probing", "Probing" },
        new string[] { "Caesar Cipher", "CaesarCipherModule" },
        new string[] { "Resistors", "resistors" },
        new string[] { "Microcontroller", "Microcontroller" },
        new string[] { "The Gamepad", "TheGamepadModule" },
        new string[] { "Monsplode, Fight!", "monsplodeFight" },
        new string[] { "Follow the Leader", "FollowTheLeaderModule" },
        new string[] { "Friendship", "FriendshipModule" },
        new string[] { "The Bulb", "TheBulbModule" },
        new string[] { "Rock-Paper-Scissors-Lizard-Spock", "RockPaperScissorsLizardSpockModule" },
        new string[] { "Square Button", "ButtonV2" },
        new string[] { "Simon Screams", "SimonScreamsModule" },
        new string[] { "Complicated Buttons", "complicatedButtonsModule" },
        new string[] { "Battleship", "BattleshipModule" },
        new string[] { "Symbolic Password", "symbolicPasswordModule" },
        new string[] { "Wire Placement", "WirePlacementModule" },
        new string[] { "Double-Oh", "DoubleOhModule" },
        new string[] { "Coordinates", "CoordinatesModule" },
        new string[] { "Rhythms", "MusicRhythms" },
        new string[] { "Neutralization", "neutralization" },
        new string[] { "Chord Qualities", "ChordQualities" },
        new string[] { "Creation", "CreationModule" },
        new string[] { "Rubik's Cube", "RubiksCubeModule" },
        new string[] { "The Clock", "TheClockModule" },
        new string[] { "LED Encryption", "LEDEnc" },
        new string[] { "Bitwise Operations", "BitOps" },
        new string[] { "Zoo", "ZooModule" },
        new string[] { "Boolean Venn Diagram", "booleanVennModule" },
        new string[] { "The Screw", "screw" },
        new string[] { "X-Ray", "XRayModule" },
        new string[] { "Color Morse", "ColorMorseModule" },
        new string[] { "Mastermind Cruel", "Mastermind Cruel" },
        new string[] { "Mastermind Simple", "Mastermind Simple" },
        new string[] { "Big Circle", "BigCircle" },
        new string[] { "Colored Switches", "ColoredSwitchesModule" },
        new string[] { "Perplexing Wires", "PerplexingWiresModule" },
        new string[] { "Monsplode Trading Cards", "monsplodeCards" },
        new string[] { "Refill that Beer!", "NeedyBeer" },
        new string[] { "Color Generator", "Color Generator" },
        new string[] { "Painting", "Painting" },
        new string[] { "Symbol Cycle", "SymbolCycleModule" },
        new string[] { "Festive Piano Keys", "FestivePianoKeys" },
        new string[] { "Flags", "FlagsModule" },
        new string[] { "Poetry", "poetry" },
        new string[] { "Button Sequence", "buttonSequencesModule" },
        new string[] { "Algebra", "algebra" },
        new string[] { "Backgrounds", "Backgrounds" },
        new string[] { "Blind Maze", "BlindMaze" },
        new string[] { "Maintenance", "maintenance" },
        new string[] { "Mortal Kombat", "mortalKombat" },
        new string[] { "The Swan", "theSwan" },
        new string[] { "European Travel", "europeanTravel" },
        new string[] { "LEGOs", "LEGOModule" },
        new string[] { "The Stopwatch", "stopwatch" },
        new string[] { "Forget Everything", "HexiEvilFMN" },
        new string[] { "The Wire", "wire" },
        new string[] { "The Sun", "sun" },
        new string[] { "Playfair Cipher", "Playfair" },
        new string[] { "Superlogic", "SuperlogicModule" },
        new string[] { "The Moon", "moon" },
        new string[] { "The Jewel Vault", "jewelVault" },
        new string[] { "Marble Tumble", "MarbleTumbleModule" },
        new string[] { "X01", "X01" },
        new string[] { "Synonyms", "synonyms" },
        new string[] { "Simon Shrieks", "SimonShrieksModule" },
        new string[] { "Guitar Chords", "guitarChords" },
        new string[] { "Calendar", "calendar" },
        new string[] { "Binary Tree", "binaryTree" },
        new string[] { "Simon's Star", "simonsStar" },
        new string[] { "Maze Scrambler", "MazeScrambler" },
        new string[] { "Mineseeker", "mineseeker" },
        new string[] { "The Number Cipher", "numberCipher" },
        new string[] { "Alphabet Numbers", "alphabetNumbers" },
        new string[] { "British Slang", "britishSlang" },
        new string[] { "Double Color", "doubleColor" },
        new string[] { "Maritime Flags", "MaritimeFlagsModule" },
        new string[] { "Know Your Way", "KnowYourWay" },
        new string[] { "Simon Samples", "simonSamples" },
        new string[] { "3D Tunnels", "3dTunnels" },
        new string[] { "The Switch", "BigSwitch" },
        new string[] { "Reverse Morse", "reverseMorse" },
        new string[] { "Manometers", "manometers" },
        new string[] { "Module Homework", "KritHomework" },
        new string[] { "Benedict Cumberbatch", "benedictCumberbatch" },
        new string[] { "Horrible Memory", "horribleMemory" },
        new string[] { "Signals", "Signals" },
        new string[] { "Boolean Maze", "boolMaze" },
        new string[] { "Coffeebucks", "coffeebucks" },
        new string[] { "Lion's Share", "LionsShareModule" },
        new string[] { "Blackjack", "KritBlackjack" },
        new string[] { "The Plunger Button", "plungerButton" },
        new string[] { "The Digit", "TheDigitModule" },
        new string[] { "The Jack-O'-Lantern", "jackOLantern" },
        new string[] { "Connection Device", "KritConnectionDev" },
        new string[] { "Instructions", "instructions" },
        new string[] { "Catchphrase", "catchphrase" },
        new string[] { "Encrypted Morse", "EncryptedMorse" },
        new string[] { "Retirement", "retirement" },
        new string[] { "101 Dalmatians", "OneHundredAndOneDalmatiansModule" },
        new string[] { "Simon Spins", "SimonSpinsModule" },
        new string[] { "Cursed Double-Oh", "CursedDoubleOhModule" },
        new string[] { "Ten-Button Color Code", "TenButtonColorCode" },
        new string[] { "Crackbox", "CrackboxModule" },
        new string[] { "Spinning Buttons", "spinningButtons" },
        new string[] { "Factory Maze", "factoryMaze" },
        new string[] { "Broken Guitar Chords", "BrokenGuitarChordsModule" },
        new string[] { "Hogwarts", "HogwartsModule" },
        new string[] { "Flip The Coin", "KritFlipTheCoin" },
        new string[] { "Numbers", "Numbers" },
        new string[] { "Cookie Jars", "cookieJars" },
        new string[] { "Free Parking", "freeParking" },
        new string[] { "Bartending", "BartendingModule" },
        new string[] { "Question Mark", "Questionmark" },
        new string[] { "SYNC-125 [3]", "sync125_3" },
        new string[] { "LED Math", "lgndLEDMath" },
        new string[] { "Simon Sounds", "simonSounds" },
        new string[] { "Harmony Sequence", "harmonySequence" },
        new string[] { "Unfair Cipher", "unfairCipher" },
        new string[] { "Melody Sequencer", "melodySequencer" },
        new string[] { "Gadgetron Vendor", "lgndGadgetronVendor" },
        new string[] { "Micro-Modules", "KritMicroModules" },
        new string[] { "Tasha Squeals", "tashaSqueals" },
        new string[] { "Digital Cipher", "digitalCipher" },
        new string[] { "Lombax Cubes", "lgndLombaxCubes" },
        new string[] { "The Stare", "StareModule" },
        new string[] { "Colored Keys", "lgndColoredKeys" },
        new string[] { "The Troll", "troll" },
        new string[] { "The Giant's Drink", "giantsDrink" },
        new string[] { "Colour Code", "colourcode" },
        new string[] { "Arithmelogic", "arithmelogic" },
        new string[] { "Simon Stops", "simonStops" },
        new string[] { "Daylight Directions", "daylightDirections" },
        new string[] { "Simon Stores", "simonStores" },
        new string[] { "Cryptic Password", "CrypticPassword" },
        new string[] { "The Block", "theBlock" },
        new string[] { "Bamboozling Button", "bamboozlingButton" },
        new string[] { "Encrypted Equations", "EncryptedEquationsModule" },
        new string[] { "Encrypted Values", "EncryptedValuesModule" },
        new string[] { "Forget Them All", "forgetThemAll" },
        new string[] { "Ordered Keys", "orderedKeys" },
        new string[] { "Blue Arrows", "blueArrowsModule" },
        new string[] { "Orange Arrows", "orangeArrowsModule" },
        new string[] { "Seven Deadly Sins", "sevenDeadlySins" },
        new string[] { "Disordered Keys", "disorderedKeys" },
        new string[] { "Boolean Keypad", "BooleanKeypad" },
        new string[] { "Calculus", "calcModule" },
        new string[] { "Pictionary", "pictionaryModule" },
        new string[] { "Antichamber", "antichamber" },
        new string[] { "Lucky Dice", "luckyDice" },
        new string[] { "Faulty Digital Root", "faultyDigitalRootModule" },
        new string[] { "Bamboozled Again", "bamboozledAgain" },
        new string[] { "Safety Square", "safetySquare" },
        new string[] { "Annoying Arrows", "lgndAnnoyingArrows" },
        new string[] { "Block Stacks", "blockStacks" },
        new string[] { "Boolean Wires", "booleanWires" },
        new string[] { "Double Arrows", "doubleArrows" },
        new string[] { "Vectors", "vectorsModule" },
        new string[] { "Forget Us Not", "forgetUsNot" },
        new string[] { "Alpha-Bits", "alphaBits" },
        new string[] { "Organization", "organizationModule" },
        new string[] { "Binary", "Binary" },
        new string[] { "Chord Progressions", "chordProgressions" },
        new string[] { "Matchematics", "matchematics" },
        new string[] { "Forget Me Now", "ForgetMeNow" },
        new string[] { "Simon Selects", "simonSelectsModule" },
        new string[] { "Robot Programming", "robotProgramming" },
        new string[] { "Needy Flower Mash", "R4YNeedyFlowerMash" },
        new string[] { "The Modkit", "modkit" },
        new string[] { "Bamboozling Button Grid", "bamboozlingButtonGrid" },
        new string[] { "Kooky Keypad", "kookyKeypadModule" },
        new string[] { "Forget Me Later", "forgetMeLater" },
        new string[] { "Geometry Dash", "geometryDashModule" },
        new string[] { "N&Ms", "NandMs" },
        new string[] { "The Hyperlink", "hyperlink" },
        new string[] { "Divisible Numbers", "divisibleNumbers" },
        new string[] { "Cruel Boolean Maze", "boolMazeCruel" },
        new string[] { "Logic Statement", "logicStatement" },
        new string[] { "14", "14" },
        new string[] { "Forget It Not", "forgetItNot" },
        new string[] { "❖", "nonverbalSimon" },
        new string[] { "Rainbow Arrows", "ksmRainbowArrows" },
        new string[] { "Digital Dials", "digitalDials" },
        new string[] { "Multicolored Switches", "R4YMultiColoredSwitches" },
        new string[] { "Naughty or Nice", "lgndNaughtyOrNice" },
        new string[] { "64", "64" },
        new string[] { "Bamboozling Time Keeper", "bamboozlingTimeKeeper" },
        new string[] { "Dreamcipher", "ksmDreamcipher" },
        new string[] { "Brainf---", "brainf" },
        new string[] { "Boxing", "boxing" },
        new string[] { "ASCII Art", "asciiArt" },
        new string[] { "Symbolic Tasha", "symbolicTasha" },
        new string[] { "Alphabetical Ruling", "alphabeticalRuling" },
        new string[] { "Microphone", "Microphone" },
        new string[] { "Widdershins", "widdershins" },
        new string[] { "Lockpick Maze", "KritLockpickMaze" },
        new string[] { "Alliances", "alliances" },
        new string[] { "Dungeon", "dungeon" },
        new string[] { "Baccarat", "baccarat" },
        new string[] { "Gatekeeper", "gatekeeper" },
        new string[] { "The Hidden Value", "theHiddenValue" },
        new string[] { "Not Capacitor Discharge", "NotCapacitorDischarge" },
        new string[] { "Not Complicated Wires", "NotComplicatedWires" },
        new string[] { "Not Keypad", "NotKeypad" },
        new string[] { "Not Morse Code", "NotMorseCode" },
        new string[] { "Not Password", "NotPassword" },
        new string[] { "Not Simaze", "NotSimaze" },
        new string[] { "Not Wire Sequence", "NotWireSequence" },
        new string[] { "Not Wiresword", "NotWiresword" },
        new string[] { "Not Who's on First", "NotWhosOnFirst" },
        new string[] { "Dungeon 2nd Floor", "dungeon2" },
        new string[] { "Quaternions", "quaternions" },
        new string[] { "Art Appreciation", "AppreciateArt" },
        new string[] { "Forget The Colors", "ForgetTheColors" },
        new string[] { "Etterna", "etterna" },
        new string[] { "Not Venting Gas", "NotVentingGas" },
        new string[] { "RPS Judging", "RPSJudging" },
        new string[] { "Triamonds", "triamonds" },
        new string[] { "Co-op Harmony Sequence", "coopharmonySequence" },
        new string[] { "Arrow Talk", "ArrowTalk" },
        new string[] { "Audio Morse", "lgndAudioMorse" },
        new string[] { "Badugi", "ksmBadugi" },
        new string[] { "Module Rick", "ModuleRick" },
        new string[] { "Remote Math", "remotemath" },
        new string[] { "Password Destroyer", "pwDestroyer" },
        new string[] { "hexOS", "hexOS" },
        new string[] { "7", "7" },
        new string[] { "More Code", "MoreCode" },
        new string[] { "DACH Maze", "DACH" },
        new string[] { "Birthdays", "birthdays" },
        new string[] { "English Entries", "EnglishEntries" },
        new string[] { "The Duck", "theDuck" },
        new string[] { "RGB Sequences", "RGBSequences" },
        new string[] { "D-CODE", "xelDcode" },
        new string[] { "RGB Arithmetic", "rgbArithmetic" },
        new string[] { "ASCII Maze", "asciiMaze" },
        new string[] { "Ultralogic", "Ultralogic" },
        new string[] { "Simon's Ultimate Showdown", "simonsUltimateShowdownModule" },
        new string[] { "Pitch Perfect", "pitchPerfect" },
        new string[] { "The Kanye Encounter", "TheKanyeEncounter" },
        new string[] { "Brown Bricks", "xelBrownBricks" },
        new string[] { "Spelling Buzzed", "SpellingBuzzed" },
        new string[] { "Mystic Maze", "mysticmaze" },
        new string[] { "Duck, Duck, Goose", "DUCKDUCKGOOSE" },
        new string[] { "Not Knob", "NotKnob" },
        new string[] { "Unfair's Revenge", "unfairsRevenge" },
        new string[] { "Unfair's Cruel Revenge", "unfairsRevengeCruel" },
        new string[] { "Regular Hexpressions", "RegularHexpressions" },
        new string[] { "Colored Buttons", "ColoredButtons" },
        new string[] { "Mechanus Cipher", "mechanusCipher" },
        new string[] { "Broken Karaoke", "xelBrokenKaraoke" },
        new string[] { "Frankenstein's Indicator", "frankensteinsIndicator" },
        new string[] { "Devilish Eggs", "devilishEggs" },
        new string[] { "Double Pitch", "DoublePitch" },
        new string[] { "Mastermind Restricted", "mastermindRestricted" },
        new string[] { "Commuting", "commuting" },
        new string[] { "Currents", "Currents" },
        new string[] { "Forget Any Color", "ForgetAnyColor" },
        new string[] { "Cosmic", "CosmicModule" },
        new string[] { "Mislocation", "mislocation" },
        new string[] { "Simon Smiles", "SimonSmiles" },
        new string[] { "Keypad Directionality", "KeypadDirectionality" },
        new string[] { "Striped Keys", "kataStripedKeys" },
        new string[] { "Black Arrows", "blackArrowsModule" },
        new string[] { "Coloured Arrows", "colouredArrowsModule" },
        new string[] { "Flashing Arrows", "flashingArrowsModule" },
        new string[] { "Simon Subdivides", "simonSubdivides" },
        new string[] { "Audio Keypad", "AudioKeypad" },
        new string[] { "Big Bean", "bigBean" },
        new string[] { "Saimoe Maze", "SaimoeMaze" },
        new string[] { "Bowling", "Bowling" },
        new string[] { "DNA Mutation", "DNAMutation" },
        new string[] { "RGB Hypermaze", "rgbhypermaze" },
        new string[] { "Complexity", "complexity" },
        new string[] { "Simon Stumbles", "simonStumbles" },
        new string[] { "Simon Swindles", "simonSwindles" },
        new string[] { "Next In Line", "NextInLine" },
        new string[] { "Free Password", "FreePassword" },
        new string[] { "The Burnt", "burnt" },
        new string[] { "Keypad Magnified", "keypadMagnified" },
        new string[] { "Ghost Movement", "ghostMovement" },
        new string[] { "Newline", "newline" },
        new string[] { "Ladders", "ladders" },
        new string[] { "Emoticon Math", "emoticonMathModule" },
        new string[] { "1D Chess", "1DChess" },
        new string[] { "Not Connection Check", "notConnectionCheck" },
        new string[] { "Not Colour Flash", "notColourFlash" },
        new string[] { "Dossier Modifier", "TDSDossierModifier" },
        new string[] { "Connect Four", "connectFourModule" },
        new string[] { "Macro Memory", "macroMemory" },
        new string[] { "Anomia", "anomia" },
        new string[] { "Colors Maximization", "colors_maximization" },
        new string[] { "Blue Whale", "blueWhale" },
        new string[] { "Hitman", "HitmanModule" },
        new string[] { "Stoichiometry", "stoichiometryModule" },
        new string[] { "Kawaiitlyn", "kawaiitlyn" },
        new string[] { "Stupid Slots", "stupidSlots" },
        new string[] { "Meteor", "meteor" },
        new string[] { "Pawns", "pawns" },
        new string[] { "Encrypted Maze", "encryptedMaze" },
        new string[] { "Fire Diamonds", "fireDiamondsModule" },
        new string[] { "Literally Malding", "literallyMalding" },
        new string[] { "Simon Shouts", "SimonShoutsModule" },
        new string[] { "Marquee Morse", "marqueeMorseModule" },
        new string[] { "Pointless Machines", "PointlessMachines" },
        new string[] { "Mastermind Restricted Cruel", "mastermindRestrictedCruel" },
        new string[] { "Warning Signs", "warningSigns" },
        new string[] { "Custom Keys", "RemoteTurnTheKeys" },
        new string[] { "Mirror", "mirror" },
        new string[] { "Insa Ilo", "insaIlo" },
        new string[] { "Tetrahedron", "tetrahedron" },
        new string[] { "Touch Transmission", "touchTransmission" },
        new string[] { "Quizbowl", "quizbowl" },
        new string[] { "Superparsing", "superparsing" },
        new string[] { "Clipping Triangles", "clippingTriangles" },
        new string[] { "Tipping Triangles", "tippingTriangles" },
        new string[] { "Not X01", "notX01" },
        new string[] { "Shogi Identification", "shogiIdentification" },
        new string[] { "Logic Chess", "logicChess" },
        new string[] { "Candy Land", "candyLand" },
        new string[] { "Melody Memory", "melodyMemory" }
    );
}
