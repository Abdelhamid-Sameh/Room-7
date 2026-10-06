using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RTL Text Mesh Pro writes Arabic using the 'Presentation Forms-B' code points (U+FE70-FEFF). Some fonts,
/// Cairo included, only have the initial / medial / final forms in their character map and NOT the
/// isolated forms (the alef in 'ال', the waw in 'و', ...), so those letters turn into a hollow square.
///
/// The plain Arabic letter (U+0621-064A) already has exactly the isolated shape, so this tool adds each
/// missing isolated code point to the font asset pointing at the plain letter's glyph.
///
/// Run it from the menu after you regenerate the Cairo font asset (regenerating wipes the patch):
///     Room 7 > Fonts > Patch Arabic isolated forms
/// </summary>
public static class ArabicFontPatcher
{
    private const string FontPath = "Assets/Fonts/Cairo/Cairo-Regular SDF.asset";

    // { isolated form (Presentation Forms-B), plain Arabic letter }
    private static readonly int[,] Map =
    {
        { 0xFE80, 0x0621 }, { 0xFE81, 0x0622 }, { 0xFE83, 0x0623 }, { 0xFE85, 0x0624 }, { 0xFE87, 0x0625 },
        { 0xFE89, 0x0626 }, { 0xFE8D, 0x0627 }, { 0xFE8F, 0x0628 }, { 0xFE93, 0x0629 }, { 0xFE95, 0x062A },
        { 0xFE99, 0x062B }, { 0xFE9D, 0x062C }, { 0xFEA1, 0x062D }, { 0xFEA5, 0x062E }, { 0xFEA9, 0x062F },
        { 0xFEAB, 0x0630 }, { 0xFEAD, 0x0631 }, { 0xFEAF, 0x0632 }, { 0xFEB1, 0x0633 }, { 0xFEB5, 0x0634 },
        { 0xFEB9, 0x0635 }, { 0xFEBD, 0x0636 }, { 0xFEC1, 0x0637 }, { 0xFEC5, 0x0638 }, { 0xFEC9, 0x0639 },
        { 0xFECD, 0x063A }, { 0xFED1, 0x0641 }, { 0xFED5, 0x0642 }, { 0xFED9, 0x0643 }, { 0xFEDD, 0x0644 },
        { 0xFEE1, 0x0645 }, { 0xFEE5, 0x0646 }, { 0xFEE9, 0x0647 }, { 0xFEED, 0x0648 }, { 0xFEEF, 0x0649 },
        { 0xFEF1, 0x064A },
    };

    [MenuItem("Room 7/Fonts/Patch Arabic isolated forms")]
    public static void PatchMenu()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[ArabicFontPatcher] Font asset not found at {FontPath}");
            return;
        }

        int added = Patch(font);
        Debug.Log($"[ArabicFontPatcher] Added {added} isolated-form characters to {font.name}.");
    }

    /// <summary>Adds every missing isolated form it can. Returns how many were added.</summary>
    public static int Patch(TMP_FontAsset font)
    {
        font.ReadFontAssetDefinition();

        int added = 0;
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            uint isolated = (uint)Map[i, 0];
            uint plain = (uint)Map[i, 1];

            if (font.characterLookupTable.ContainsKey(isolated)) continue;

            TMP_Character plainChar;
            if (!font.characterLookupTable.TryGetValue(plain, out plainChar)) continue;

            font.characterTable.Add(new TMP_Character(isolated, plainChar.glyph));
            added++;
        }

        if (added > 0)
        {
            font.ReadFontAssetDefinition();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        return added;
    }
}
