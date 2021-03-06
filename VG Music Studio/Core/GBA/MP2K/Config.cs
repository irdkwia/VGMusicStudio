﻿using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Properties;
using Kermalis.VGMusicStudio.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K
{
    internal class Config : Core.Config
    {
        public readonly byte[] ROM;
        public readonly EndianBinaryReader Reader;
        public string GameCode;
        public byte Version;

        public string Name;
        public int[] SongTableOffsets;
        public long[] SongTableSizes;
        public int SampleRate;
        public ReverbType ReverbType;
        public byte Reverb;
        public byte Volume;
        public bool HasGoldenSunSynths;
        public bool HasPokemonCompression;

        public Config(byte[] rom)
        {
            const string configFile = "MP2K.yaml";
            using (StreamReader fileStream = File.OpenText(Util.Utils.CombineWithBaseDirectory(configFile)))
            {
                string gcv = string.Empty;
                try
                {
                    ROM = rom;
                    Reader = new EndianBinaryReader(new MemoryStream(rom));
                    GameCode = Reader.ReadString(4, 0xAC);
                    Version = Reader.ReadByte(0xBC);
                    gcv = $"{GameCode}_{Version:X2}";
                    var yaml = new YamlStream();
                    yaml.Load(fileStream);

                    var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
                    YamlMappingNode game;
                    try
                    {
                        game = (YamlMappingNode)mapping.Children.GetValue(gcv);
                    }
                    catch (BetterKeyNotFoundException)
                    {
                        throw new Exception(string.Format(Strings.ErrorParseConfig, configFile, Environment.NewLine + string.Format(Strings.ErrorMLSSMP2KMissingGameCode, gcv)));
                    }

                    YamlNode nameNode = null,
                        songTableOffsetsNode = null,
                        songTableSizesNode = null,
                        sampleRateNode = null,
                        reverbTypeNode = null,
                        reverbNode = null,
                        volumeNode = null,
                        hasGoldenSunSynthsNode = null,
                        hasPokemonCompression = null;
                    void Load(YamlMappingNode gameToLoad)
                    {
                        if (gameToLoad.Children.TryGetValue("Copy", out YamlNode node))
                        {
                            YamlMappingNode copyGame;
                            try
                            {
                                copyGame = (YamlMappingNode)mapping.Children.GetValue(node);
                            }
                            catch (BetterKeyNotFoundException ex)
                            {
                                throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + string.Format(Strings.ErrorMLSSMP2KCopyInvalidGameCode, ex.Key)));
                            }
                            Load(copyGame);
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(Name), out node))
                        {
                            nameNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(SongTableOffsets), out node))
                        {
                            songTableOffsetsNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(SongTableSizes), out node))
                        {
                            songTableSizesNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(SampleRate), out node))
                        {
                            sampleRateNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(ReverbType), out node))
                        {
                            reverbTypeNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(Reverb), out node))
                        {
                            reverbNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(Volume), out node))
                        {
                            volumeNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(HasGoldenSunSynths), out node))
                        {
                            hasGoldenSunSynthsNode = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(HasPokemonCompression), out node))
                        {
                            hasPokemonCompression = node;
                        }
                        if (gameToLoad.Children.TryGetValue(nameof(Playlists), out node))
                        {
                            var playlists = (YamlMappingNode)node;
                            foreach (KeyValuePair<YamlNode, YamlNode> kvp in playlists)
                            {
                                string name = kvp.Key.ToString();
                                var songs = new List<Song>();
                                foreach (KeyValuePair<YamlNode, YamlNode> song in (YamlMappingNode)kvp.Value)
                                {
                                    long songIndex = Util.Utils.ParseValue(string.Format(Strings.ConfigKeySubkey, nameof(Playlists)), song.Key.ToString(), 0, long.MaxValue);
                                    if (songs.Any(s => s.Index == songIndex))
                                    {
                                        throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + string.Format(Strings.ErrorMLSSMP2KSongRepeated, name, songIndex)));
                                    }
                                    songs.Add(new Song(songIndex, song.Value.ToString()));
                                }
                                Playlists.Add(new Playlist(name, songs));
                            }
                        }
                    }

                    Load(game);

                    if (nameNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(Name), null);
                    }
                    Name = nameNode.ToString();
                    if (songTableOffsetsNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(SongTableOffsets), null);
                    }
                    string[] songTables = songTableOffsetsNode.ToString().SplitSpace(StringSplitOptions.RemoveEmptyEntries);
                    int numSongTables = songTables.Length;
                    if (numSongTables == 0)
                    {
                        throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + string.Format(Strings.ErrorConfigKeyNoEntries, nameof(SongTableOffsets))));
                    }
                    if (songTableSizesNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(SongTableSizes), null);
                    }
                    string[] sizes = songTableSizesNode.ToString().SplitSpace(StringSplitOptions.RemoveEmptyEntries);
                    if (sizes.Length != numSongTables)
                    {
                        throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + string.Format(Strings.ErrorMLSSMP2KSongTableCounts, nameof(SongTableSizes), nameof(SongTableOffsets))));
                    }
                    SongTableOffsets = new int[numSongTables];
                    SongTableSizes = new long[numSongTables];
                    int maxOffset = rom.Length - 1;
                    for (int i = 0; i < numSongTables; i++)
                    {
                        SongTableSizes[i] = Util.Utils.ParseValue(nameof(SongTableSizes), sizes[i], 1, maxOffset);
                        SongTableOffsets[i] = (int)Util.Utils.ParseValue(nameof(SongTableOffsets), songTables[i], 0, maxOffset);
                    }
                    if (sampleRateNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(SampleRate), null);
                    }
                    SampleRate = (int)Util.Utils.ParseValue(nameof(SampleRate), sampleRateNode.ToString(), 0, Utils.FrequencyTable.Length - 1);
                    if (reverbTypeNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(ReverbType), null);
                    }
                    ReverbType = Util.Utils.ParseEnum<ReverbType>(nameof(ReverbType), reverbTypeNode.ToString());
                    if (reverbNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(Reverb), null);
                    }
                    Reverb = (byte)Util.Utils.ParseValue(nameof(Reverb), reverbNode.ToString(), byte.MinValue, byte.MaxValue);
                    if (volumeNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(Volume), null);
                    }
                    Volume = (byte)Util.Utils.ParseValue(nameof(Volume), volumeNode.ToString(), 0, 15);
                    if (hasGoldenSunSynthsNode == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(HasGoldenSunSynths), null);
                    }
                    HasGoldenSunSynths = Util.Utils.ParseBoolean(nameof(HasGoldenSunSynths), hasGoldenSunSynthsNode.ToString());
                    if (hasPokemonCompression == null)
                    {
                        throw new BetterKeyNotFoundException(nameof(HasPokemonCompression), null);
                    }
                    HasPokemonCompression = Util.Utils.ParseBoolean(nameof(HasPokemonCompression), hasPokemonCompression.ToString());

                    // The complete playlist
                    if (!Playlists.Any(p => p.Name == "Music"))
                    {
                        Playlists.Insert(0, new Playlist(Strings.PlaylistMusic, Playlists.SelectMany(p => p.Songs).Distinct().OrderBy(s => s.Index)));
                    }
                }
                catch (BetterKeyNotFoundException ex)
                {
                    throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + string.Format(Strings.ErrorConfigKeyMissing, ex.Key)));
                }
                catch (InvalidValueException ex)
                {
                    throw new Exception(string.Format(Strings.ErrorMLSSMP2KParseGameCode, gcv, configFile, Environment.NewLine + ex.Message));
                }
                catch (YamlDotNet.Core.YamlException ex)
                {
                    throw new Exception(string.Format(Strings.ErrorParseConfig, configFile, Environment.NewLine + ex.Message));
                }
            }
        }

        public override string GetSongName(long index)
        {
            return index.ToString();
        }

        public override void Dispose()
        {
            Reader.Dispose();
        }
    }
}
