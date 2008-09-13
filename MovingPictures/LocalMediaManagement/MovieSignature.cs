﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace MediaPortal.Plugins.MovingPictures.LocalMediaManagement
{
  public class MovieSignature
  {
    
    public MovieSignature(MediaMatch mm)
    {
      parseMediaMatch(mm);
    }

    public MovieSignature(string inputStr)
    {
      parseSignature(inputStr);
    }

    public string Title
    {
      get { return _title; }
      set { _title = value; }
    } private string _title;

    public int Year
    {
      get { return _year; }
      set { _year = value; }
    } private int _year;
    
    private void parseMediaMatch (MediaMatch mm) {
      string sourceStr;
      bool preferFolder = (bool)MovingPicturesCore.SettingsManager["importer_prefer_foldername"].Value;

      if (mm.LocalMedia == null || mm.LocalMedia.Count == 0)
        return;
      else if (mm.FolderHint)
      { // ## If FolderHint is true we -can- use the foldername to create the searchstring
        if (mm.LocalMedia.Count > 1 || preferFolder)
        { // if it's multi-part media use the folder name
          // if the preferFolder value is true (one movie one folder) also use the folder name
          sourceStr = parseFolderName(mm.LocalMedia[0].File.Directory);
        }
        else
        {
          // If preferFolder is false we always use the filename for single part media
          sourceStr = parseFileName(mm.LocalMedia[0].File);
        }
      }
      // ## We can't use the foldername because it contains different movies 
      else
      {
        if (mm.LocalMedia.Count > 1)
        { // if it's multi-part media  use filename with stack marker cleaning
          sourceStr = parseFileName(mm.LocalMedia[0].File, true);
        }
        else
        {
          // just use filename
          sourceStr = parseFileName(mm.LocalMedia[0].File);
        }
      }

      parseSignature(sourceStr);
    }
    
    // Cleans up a filename (without stack markers)
    public static string parseFileName(FileInfo file)
    {
      return parseFileName(file, false);
    }

    // Cleans up a filename for movie name matching. 
    // Removes extension and cleans stack markers.
    private static string parseFileName(FileInfo file, bool filterStackMatch)
    {
      string str = MovieImporter.RemoveFileExtension(file);

      if (filterStackMatch)
      {
        // Remove stack markers
        Regex rxParser = new Regex(MovieImporter.rxMultiPartClean, RegexOptions.IgnoreCase);
        Match match = rxParser.Match(str);
        if (match.Success)
        {
          // if we have a match on this regexp we can just replace the matches.
          str = rxParser.Replace(str, "");
        }
        else
        {
          // if we don't have match we should remove just one character.
          str = str.Substring(0, (str.Length - 1));
        }
      }

      return str;
    }

    // Cleans up a directory name for movie name matching. 
    public static string parseFolderName(DirectoryInfo dir)
    {
      return dir.Name;
    }

    // cleans a string up for movie name matching.
    private void parseSignature(string inputStr)
    {
      string sig = inputStr;
      int year;

      // Phase #1: Spacing
      // if there are periods or underscores, assume the period is replacement for spaces.
      sig = sig.Replace('.', ' ');
      sig = sig.Replace('_', ' ');

      // Phase #2: Cleaning (remove noise)
      sig = filterNoise(sig);
      
      // Phase #3: Year detection
      this.Title = filterYearFromTitle(sig, out year);
      this.Year = year;
    }

    // Filters "noise" from the input string
    public static string filterNoise(string input)
    {
      string rxPattern = MovingPicturesCore.SettingsManager["importer_filter"].Value.ToString();
      Regex rxParser = new Regex(rxPattern, RegexOptions.IgnoreCase);
      return rxParser.Replace(input, "");
    }
    
    // Separates the year from the title string (if applicable)
    public static string filterYearFromTitle(string input, out int year)
    {
      string rtn = input;
      year = 0;

      // if there is a four digit number that looks like a year, parse it out
      Regex rxParser = new Regex(MovieImporter.rxYearScan);
      Match match = rxParser.Match(rtn);
      if (match.Success)
      {
        // set the year
        year = int.Parse(match.Groups[2].Value);
        // check if it's really a year value
        if (year > 1900 && year < DateTime.Now.Year + 2)
        {
          // clean the possible left overs from the title
          rtn = match.Groups[1].Value.TrimEnd('(', '[').Trim();
        } else {
          // year check failed so reset it to 0
          year = 0;
        }
      }

      // return the title
      return rtn;
    }

  }
}