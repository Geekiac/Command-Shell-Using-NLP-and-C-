// Program.cs - Steven Smith      2017 January 02
//
// Natural Language Processing(NLP) of text entered at the command line and
// the execution of commands in an OS shell.
//
// The entry method is CSharp_Shell_NLP.Main
//
// The system is capable of executing commands on Windows (Only)
//
// NOTE: The use of RegEx and case insensitive matches means that the correct
// case of filename or directory name is always available so this is used to 
// build the commands.  This is not necessary on Windows but is used to 
// demonstrate that this would be easy to do if on a posix system.
//
// Windows DOS commands supported - see Program.TranslationRules.
//       dir X:
//       dir Y:*.X
//       copy X Y
//       dir X
//       more X
//       cd
//       cd X
//       mkdir X
//       rmdir X
//       ver
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CSharp_Shell_NLP
{
    /// <summary>
    /// The Rule class represents either a simplified rule or a translation rule.
    /// The rules are strings mapping From one string To another.
    /// </summary>
    public abstract class Rule
    {
        /// <summary>
        /// The From mapping
        /// </summary>
        public string From { get; private set; }
        /// <summary>
        /// The To mapping
        /// </summary>
        public string To { get; private set; }

        /// <summary>
        /// Used to construct a new Rule
        /// </summary>
        public Rule(string from, string to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// This class represents a Simplified Rule
    /// </summary>
    public class SimplifiedRule : Rule
    {
        /// <summary>
        /// Used to construct a new Rule
        /// </summary>
        public SimplifiedRule(string from, string to) : base(from, to) { }

        /// <summary>
        /// Determines if the simplifiedWords start with the From part of the rule 
        /// mapping - Used with SimplifiedRules
        /// </summary>
        /// <param name="simplifiedWords">The words to try and match with</param>
        /// <returns>true if the rule starts with the From part of the rule and false 
        /// otherwise</returns>
        public bool IsMatch(string simplifiedWords) =>
            simplifiedWords.StartsWith(From, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// This method uses simplifiedRules to simplify the input line for later matching
        /// with a command string
        /// </summary>
        /// <param name="line">The line to simplify</param>
        /// <param name="simplifiedRules">The simplified rules to apply</param>
        /// <returns>A simplified string</returns>
        public static string SimplifyWords(string line, IEnumerable<SimplifiedRule> simplifiedRules)
        {
            var currentLine = line;
            var simplifiedWords = new List<string>();
            // process currentLine until no words are left
            while (currentLine != string.Empty)
            {
                // Try and find a matching rule, attempting to match the longest
                var rule = simplifiedRules.FirstOrDefault(sr => sr.IsMatch(currentLine));
                if (rule == null)
                {
                    // if no rule matched the first word is removed from the currentLine
                    // added to simplifedWords and current line is replace with the remaining
                    // words
                    var words = currentLine.Split(new[] { ' ' }, 2);
                    AddWordToList(words[0], simplifiedWords);
                    currentLine = (words.Length > 1) ? words[1] : string.Empty;
                }
                else
                {
                    // if a rule matches, the mapped words are added to simplified words and the
                    // current line has the rule.From words removed from the beginning
                    AddWordToList(rule.To, simplifiedWords);
                    currentLine = SubstringOrEmpty(currentLine, rule.From.Length + 1);
                }
            }
            // a space seperated string is created from the simplifiedWords list
            return string.Join(" ", simplifiedWords);
        }

        /// <summary>
        /// Adds a word to the list if the word is not null or whitespace
        /// </summary>
        /// <param name="word">Word to add to the list</param>
        /// <param name="list">The list to add the word to</param>
        private static void AddWordToList(string word, List<string> list)
        {
            if (!string.IsNullOrWhiteSpace(word))
                list.Add(word.Trim());
        }

        /// <summary>
        /// Substrings a string, without the exception, as checks the length of the string
        /// and returns the empty string if the startIndex is out of range
        /// </summary>
        /// <param name="words">Words to substring</param>
        /// <param name="startIndex">The index to start at</param>
        /// <returns>The substring or the empty string if the startIndex is out of range</returns>
        private static string SubstringOrEmpty(string words, int startIndex) =>
            startIndex > (words.Length - 1) ? string.Empty : words.Substring(startIndex);
    }

    public class TranslationRule : Rule
    {
        /// <summary>
        /// Holds the regular expression that From represents.
        /// </summary>
        private readonly Regex _fromRegEx;

        /// <summary>
        /// Used to construct a new Rule
        /// </summary>
        public TranslationRule(string from, string to) : base(from, to)
        {
            _fromRegEx = new Regex(from, RegexOptions.IgnoreCase);
        }
        
        /// <summary>
        /// Determines if the simplifiedWords match the regular expression contained in 
        /// the From part of the rule mapping - Used with TranslationRules
        /// </summary>
        /// <param name="simplifiedWords">The words to try and match with</param>
        /// <returns>true if the rule matches the regular expression contained in  
        /// the From part of the rule and false otherwise</returns>
        public bool IsRegExMatch(string simplifiedWords) => _fromRegEx.IsMatch(simplifiedWords);

        /// <summary>
        /// Maps the simplifiedWords to a shell command using the regular expression contained in 
        /// the From part of the rule mapping.  The regular expression grouping construct is used
        /// to determine the parameters to the command  and a format string (To) is used to place 
        /// the parameters into the command - Used with TranslationRules
        /// </summary>
        /// <param name="simplifiedWords">The simplified words to map to a shell command</param>
        /// <returns>A command string to execute</returns>
        public string ToCommandString(string simplifiedWords) =>
            string.Format(To, _fromRegEx.Match(simplifiedWords)
                                        .Groups.Cast<Group>()
                                        .Skip(1) // Ignore the first group as this is the entire match
                                        .Select(g => g.Value).ToArray());
    }

    /// <summary>
    /// This is the main class in the application and contains the entry method Main
    /// </summary>
    public class CSharp_Shell_NLP
    {
        /// <summary>
        /// The SimplifiedRules used to simplify the input string
        /// NOTE: The rules are sorted by descending To length and then by descending From length.
        /// This is necessary as the SimplifiedRules are matched with the input string using the
        /// start of the input string and we want "these files" to match with "these" before "the"
        /// </summary>
        public static SimplifiedRule[] SimplifiedRules = new[]
        {
            // equivalent phrases - like synonyms
            new SimplifiedRule("directory to", "directory"),
            new SimplifiedRule("disk in drive", "drive"),
            new SimplifiedRule("disk in", "drive"),
            new SimplifiedRule("what files", "files"),
            new SimplifiedRule("everything", "all files"),
            new SimplifiedRule("any files", "all files"),
            new SimplifiedRule("files contents", "contents files"),
            new SimplifiedRule("to make", "make"),
            new SimplifiedRule("to remove", "remove"),
            new SimplifiedRule("to change", "change"),
            new SimplifiedRule("to copy", "copy"),
            new SimplifiedRule("to show", "show"),
            // synonyms - equivalent words
            new SimplifiedRule("disk", "drive"),
            new SimplifiedRule("file", "files"),
            new SimplifiedRule("every", "all"),
            new SimplifiedRule("content", "contents"),
            new SimplifiedRule("in", "on"),
            new SimplifiedRule("create", "make"),
            new SimplifiedRule("delete", "remove"),
            new SimplifiedRule("switch", "change"),
            new SimplifiedRule("bye", "quit"),
            new SimplifiedRule("exit", "quit"),
            new SimplifiedRule("running", "using"),
            new SimplifiedRule("path", "directory"),
            // stop phrases - phrases that are ignored
            new SimplifiedRule("i would", ""),
            new SimplifiedRule("can i", ""),
            new SimplifiedRule("can you", ""),
            new SimplifiedRule("could i", ""),
            new SimplifiedRule("could you", ""),
            new SimplifiedRule("would you", ""),
            new SimplifiedRule("will you", ""),
            new SimplifiedRule("give me", ""),
            new SimplifiedRule("like you to", ""),
            new SimplifiedRule("like to", ""),
            new SimplifiedRule("am i", ""),
            new SimplifiedRule("i am", ""),
            // stop words - words that are ignored
            new SimplifiedRule("please", ""),
            new SimplifiedRule("me", ""),
            new SimplifiedRule("the", ""),
            new SimplifiedRule("is", ""),
            new SimplifiedRule("are", ""),
            new SimplifiedRule("a", ""),
            new SimplifiedRule("there", ""),
            new SimplifiedRule("these", ""),
            new SimplifiedRule("any", ""),
            new SimplifiedRule("like", ""),
            new SimplifiedRule("of", ""),
            new SimplifiedRule("see", ""),
            new SimplifiedRule("list", ""),
            new SimplifiedRule("show", ""),
            new SimplifiedRule("tell", ""),
            new SimplifiedRule("what", ""),
            new SimplifiedRule("which", ""),
            new SimplifiedRule("you", ""),
        }.Select(r => new { Rule = r, FromLen = r.From.Length, ToLen = r.To.Length })
         .OrderByDescending(sr => sr.ToLen)
         .OrderByDescending(sr => sr.FromLen)
         .Select(r => r.Rule)
         .ToArray();

        /// <summary>
        /// The TranslationRules used to map the simplified version of the input
        /// to the command.  Regular expressions are held in the From mapping and 
        /// format strings are held in the To mapping.S
        /// </summary>
        public static TranslationRule[] TranslationRules = new []
        {
            new TranslationRule("^quit$", "quit"),
            new TranslationRule("^all files on drive (.+)$","dir {0}:"),
            new TranslationRule("^(.+?) files on drive (.+)$","dir {1}:*.{0}"),
            new TranslationRule("^copy files from (.+?) to (.+)$","copy {0} {1}"),
            new TranslationRule("^files on directory (.+)$","dir {0}"),
            new TranslationRule("^contents files (.+)$","c:\\windows\\system32\\more {0}"),
            new TranslationRule("^current directory$","cd"),
            new TranslationRule("^change directory (.+)$","cd {0}"),
            new TranslationRule("^make directory (.+)$","mkdir {0}"),
            new TranslationRule("^remove directory (.+)$","rmdir {0}"),
            new TranslationRule("^os using$","ver"),
        };

        /// <summary>
        /// This is the entry method for the application
        /// </summary>
        /// <param name="args">Arguments passed from the command line
        /// NOTE: no arguments are passed to this application</param>
        public static void Main(string[] args)
        {
            // Determine the path to cmd.exe
            var cmdExeFilePath = Environment.GetEnvironmentVariable("COMSPEC");
            // Keep looping until break
            while (true)
            {
                Console.Write("Command -> ");
                // Reads from the command line and then executes SimplifyWords twice
                var simplifiedWords = SimplifiedRule.SimplifyWords(Console.ReadLine(), SimplifiedRules);
                // Obtain the command that matches the simplifiedWords
                var command = TranslationRules
                                  .FirstOrDefault(tr => tr.IsRegExMatch(simplifiedWords))
                                  ?.ToCommandString(simplifiedWords);
                // if no translation rule matched
                if (command == null)
                    Console.WriteLine("I do not understand: {0}", simplifiedWords);
                // if quitting the application
                else if (command == "quit")
                    break; // Exits the while(true) loop.
                // if changing directory cannot use cmd.exe as the directory change is lost when 
                // cmd exits
                else if (command.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
                    Directory.SetCurrentDirectory(command.Substring(3));
                else
                    // Open a cmd shell and execute the command
                    Process.Start(new ProcessStartInfo(cmdExeFilePath, "/c " + command)
                                  {
                                      UseShellExecute = false
                                  }).WaitForExit();
            }
        }
    }
}
