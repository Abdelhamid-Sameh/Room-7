using System;
using UnityEngine;

/// <summary>
/// The radio's tracks and the names it shows for them, in ONE place - both scenes point at the same
/// asset (Assets/Audio/Music/RadioPlaylist.asset), so a name only has to be fixed once.
///
/// To rename a track: select the asset, change 'Display Name' on that entry. Leave it empty to show
/// the audio file's own name. (Unity's Inspector shows Arabic unjoined and left-to-right - that is
/// only how the Inspector draws it; the game shapes it correctly.)
/// </summary>
[CreateAssetMenu(fileName = "RadioPlaylist", menuName = "Room 7/Radio Playlist")]
public class RadioPlaylist : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AudioClip clip;

        [Tooltip("What the radio shows. Leave empty to use the audio file's name.")]
        public string displayName;
    }

    [SerializeField] private Entry[] tracks = new Entry[0];

    public int Count => tracks != null ? tracks.Length : 0;

    public AudioClip ClipAt(int index)
    {
        return index >= 0 && index < Count ? tracks[index].clip : null;
    }

    public string NameAt(int index)
    {
        if (index < 0 || index >= Count) return "";

        Entry e = tracks[index];
        if (!string.IsNullOrWhiteSpace(e.displayName)) return e.displayName;
        return e.clip != null ? e.clip.name : $"Track {index + 1}";
    }
}
