using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Infiniminer
{
    /* Loads in a datafile consisting of key/value pairs, in the format of
     * "key = value", which can be read out through the Data dictionary.
     */

    public class DatafileWriter
    {
        public string fullContent="";
        public Dictionary<string, string> Data = new Dictionary<string, string>();
        /*public Dictionary<string, string> Data
        {
            get { return dataDict; }
        }*/

        public DatafileWriter(string filename)
        {
            Data = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(filename))
                {
                    string defaultConfig;
                    
                    if (Path.GetFileName(filename).ToLower() == "server.config.txt")
                    {
                        defaultConfig = @"# Infiniminer Server Configuration
# Server settings
name=Infiniminer Server
maxplayers=16
port=5565
publiclist=true

# Game settings
siege=0
sandbox=false
tnt=true
lava=true
water=true
artifacts=true

# Map settings
mapsize=64
regionsize=16
winningcash=6

# Advanced settings
autosave=1
backupinterval=300
physicsthreads=1";
                    }
                    else // client.config.txt
                    {
                        defaultConfig = @"# Infiniminer Client Configuration

# Display settings
width=1280
height=720
windowmode=windowed
showfps=false
pretty=true
light=true

# Control settings
yinvert=false
sensitivity=5
mousesensitivity=1.0

# Audio settings
nosound=false
volume=1.0

# Player settings
handle=Player

# Team settings
red_name=Red
blue_name=Blue
red=237,28,36
blue=0,0,255

# Performance settings
inputlagfix=false";
                    }

                    File.WriteAllText(filename, defaultConfig);
                }

                FileStream file = new FileStream(filename, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(file);

                fullContent = "";
                string line = sr.ReadLine();
                while (line != null)
                {
                    fullContent += line;
                    string[] args = line.Split("=".ToCharArray(), 2);
                    if (args.Length == 2 && line[0] != '#')
                    {
                        Data[args[0].Trim()] = args[1].Trim();
                    }
                    line = sr.ReadLine();
                }

                sr.Close();
                file.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading configuration file {filename}: {e.Message}");
                Console.WriteLine("Using default configuration values.");
                
                // Set default values
                Data["name"] = "Infiniminer Server";
                Data["maxplayers"] = "16";
                Data["port"] = "5565";
                Data["publiclist"] = "true";
                Data["siege"] = "0";
                Data["sandbox"] = "false";
                Data["tnt"] = "true";
                Data["lava"] = "true";
                Data["water"] = "true";
                Data["artifacts"] = "true";
                Data["mapsize"] = "64";
                Data["regionsize"] = "16";
                Data["winningcash"] = "6";
                Data["autosave"] = "1";
                Data["backupinterval"] = "300";
                Data["physicsthreads"] = "1";
            }
        }

        public int WriteChanges(string filename)
        {
            try
            {
                // Ensure we can write to the file first
                try
                {
                    using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        // Just testing write access
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Cannot write to {filename} - Check file permissions");
                    return 0;
                }

                // Read existing content
                Dictionary<string, string> existingData = new Dictionary<string, string>();
                List<string> comments = new List<string>();
                bool changes = false;

                if (File.Exists(filename))
                {
                    string[] lines = File.ReadAllLines(filename);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        {
                            comments.Add(line);
                            continue;
                        }

                        string[] args = line.Split('=', 2);
                        if (args.Length == 2)
                        {
                            string key = args[0].Trim();
                            string value = args[1].Trim();
                            existingData[key] = value;
                        }
                    }
                }

                // Build new content
                StringBuilder contentToWrite = new StringBuilder();
                
                // Preserve comments
                foreach (string comment in comments)
                {
                    contentToWrite.AppendLine(comment);
                }

                // Write all settings
                foreach (var kvp in Data)
                {
                    if (!existingData.ContainsKey(kvp.Key) || existingData[kvp.Key] != kvp.Value)
                    {
                        changes = true;
                    }
                    contentToWrite.AppendLine($"{kvp.Key}={kvp.Value}");
                }

                if (changes)
                {
                    // Write with explicit file share mode
                    using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.Write(contentToWrite.ToString());
                        sw.Flush();
                    }
                    return 2;
                }
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error writing to {filename}: {e.Message}");
                Console.WriteLine(e.StackTrace);
            return 0;
            }
        }

    }
}
