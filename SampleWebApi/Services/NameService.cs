using System;
using System.Collections.Generic;
using System.IO;

namespace SampleWebApi.Services
{
    public class NameService
    {
        private readonly string[] _words;
        private readonly string[] _firstNames;
        private readonly string[] _lastNames;
        private readonly Random _numGen;

        public NameService()
        {
            _words = ParseWordList("Resources/words.txt");
            _firstNames = ParseWordList("Resources/firstnames.txt");
            _lastNames = ParseWordList("Resources/lastnames.txt");
            _numGen = new Random((int)(DateTime.Now.Ticks & int.MaxValue));
        }

        public string GetWord() => _words[_numGen.Next(_words.Length)];
        public string GetFirstName() => _firstNames[_numGen.Next(_firstNames.Length)];
        public string GetLastName() => _lastNames[_numGen.Next(_lastNames.Length)];
        public DateTimeOffset GetBirthDate() => DateTimeOffset.Now.Date.AddDays(0 - _numGen.Next(6500, 31000));
        public int GetYear(int minYear) => minYear + _numGen.Next(1 + DateTimeOffset.Now.Year - minYear);
        public string GetWords(int minWords, int maxWords)
        {
            var wordCount = _numGen.Next(minWords, maxWords);
            var words = new List<string>();
            for (var i = 0; i < wordCount; i++)
            {
                words.Add(GetWord());
            }

            return string.Join(' ', words);
        }

        private string[] ParseWordList(string fileName) =>
            File.ReadAllLines(fileName);
    }
}
