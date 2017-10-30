﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace OpenTraceRT {
    static class PatternChecker {
        public static bool IsValidIP(string input) {         
            //string hostPattern = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$"; ;
            if (!CheckPattern(input, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b")) {
                Console.WriteLine("Invalid Input");
                return false;
            }
            return true;
        }

        public static bool IsTraceComplete(string input) {
            if (!CheckPattern(input, @"Trace complete.")) {
                return false;
            }
            return true;
        }

        private static bool CheckPattern(string input, string pattern) {
            bool isValid = true;
            Regex rgx = new Regex(pattern);
            if (!rgx.IsMatch(input)) {
                isValid = false;
            }
            return isValid;
        }
    }
}
