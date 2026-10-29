using System;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.Text;

namespace RY.Compiler
{
    public class Interpreter
    {
        private readonly HttpModule _httpModule = new();
        private readonly MathModule _mathModule = new();
        private readonly SysModule _sysModule = new();
        private readonly BModule  _BModule = new();

        private readonly List<Token> _tokens;
        private int _position;

        private bool _dsr = false; // did single reset
        
        private readonly Dictionary<string, object> _variables = new();
        private readonly Dictionary<string, int> _userFunctions = new();
        private readonly Stack<int> _callStack = new();
        private bool _iP = false; // is paused
        private int _pP = -1; // paused pos
        private Dictionary<string, object> _pausedVariables = new();

        public object? ReturnValue { get; private set; } = null;

        public Interpreter(List<Token> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }

        public void Interpret()
        {
            ReturnValue = null;
            ScanFunctions();

            while (_position < _tokens.Count)
            {
                if (_iP) // if paused, wait
                {
                    Thread.Sleep(50);
                    continue;
                }

                Token current = _tokens[_position];

                // 1. VARIABLE ASSIGNMENT
                if (current.Type == TokenType.Identifier && _position + 1 < _tokens.Count && _tokens[_position + 1].Value == "=")
                {
                    string varName = current.Value;
                    _position += 2; // skip identifier and '='

                    if (_position < _tokens.Count)
                    {
                        object value = EvaluateExpression();
                        _variables[varName] = value;
                    }         
                    continue;
                }

                // 2. HANDLE PRINT (Universal function)
                if (current.Value == "print")
                {
                    _position++; // skip print
                    
                    if (_position < _tokens.Count && _tokens[_position].Value == "(")
                    {
                        _position++; // skip bracket
                        
                        if (_position < _tokens.Count && _tokens[_position].Value != ")")
                        {
                            object valToPrint = EvaluateExpression();
                            Console.WriteLine(valToPrint);
                        }
                        else
                        {
                            Console.WriteLine();
                        }
                        
                        if (_position < _tokens.Count && _tokens[_position].Value == ")")
                        {
                            _position++; // skip bracket
                        }
                    }
                    continue;
                }

                // 3. HANDLE RETURN
                if (current.Type == TokenType.Keyword && current.Value == "return")
                {
                    _position++; // skip 'return'

                    bool hasParen = false;
                    if (_position < _tokens.Count && _tokens[_position].Value == "(")
                    {
                        hasParen = true;
                        _position++; // skip bracket
                    }

                    if (_position < _tokens.Count && _tokens[_position].Value != ")")
                    {
                        ReturnValue = EvaluateExpression();
                    }

                    if (hasParen && _position < _tokens.Count && _tokens[_position].Value == ")")
                    {
                        _position++; // skip bracket
                    }
                    continue;
                }

                // 4. HANDLE IF / ELSEIF / ELSE
                if (current.Type == TokenType.Keyword && (current.Value == "if" || current.Value == "elseif"))
                {
                    bool isIf = current.Value == "if";
                    _position++; // skip 'if' or 'elseif'

                    bool conditionMet = EvaluateCondition(out int advancedPositions);
                    _position += advancedPositions;

                    if (_position < _tokens.Count && _tokens[_position].Value == "do")
                    {
                        _position++; // skip 'do'
                    }

                    if (conditionMet)
                    {
                        // execute block normally until 'stop'
                    }
                    else
                    {
                        SkipBlock(); // skip statements inside this block until matching 'stop'
                    }
                    continue;
                }

                // 5. HANDLE FUNCTION.RETURN()
                if (current.Value == "function" && _position + 2 < _tokens.Count && 
                    _tokens[_position + 1].Value == "." && _tokens[_position + 2].Value == "return")
                {
                    _position += 3; // skip 'function', '.', 'return'
                    bool loopInfinitely = false;

                    if (_position < _tokens.Count && _tokens[_position].Value == "(")
                    {
                        _position++; // skip bracket
        
                        if (_position < _tokens.Count)
                        {
                            Token argToken = _tokens[_position];
                            if (argToken.Value == "infinite")
                            {
                                loopInfinitely = true;
                                _position++;
                            }
                            else if (argToken.Value == "finite")
                            {
                                loopInfinitely = false;
                                _position++;
                            }
                        }

                        if (_position < _tokens.Count && _tokens[_position].Value == ")") { _position++; /* skip bracket */ }
                    }

                    if (loopInfinitely)
                    {
                        _variables.Clear();
                        _position = 0;
                        Console.WriteLine("\n[RY System]: Script restarting infinitely via function.return(infinite)\n");
                    }
                    else
                    {
                        if (!_dsr)
                        {
                            _dsr = true;
                            _variables.Clear();
                            _position = 0;
                            Console.WriteLine("\n[RY System]: Script restarting once via function.return(finite)\n");
                        }
                        else
                        {
                            Console.WriteLine("\n[RY System]: function.return(finite) skipped (already performed single restart)\n");
                        }
                    }
                    continue;
                }

                // 6. HANDLE FUNCTION.WAIT()
                if (current.Value == "function" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == "." && _tokens[_position + 2].Value == "wait")
                {
                    _position += 3; // skip function + operator + wait
                    int delayMs = 0;

                    if (_position < _tokens.Count && _tokens[_position].Value == "(")
                    {
                        _position++; // skip bracket
                        if (_position < _tokens.Count && _tokens[_position].Value != ")")
                        {
                            delayMs = Convert.ToInt32(EvaluateExpression());
                        }
                        if (_position < _tokens.Count && _tokens[_position].Value == ")")
                        {
                            _position++; // skip bracket
                        }
                    }

                    Thread.Sleep(delayMs);
                    continue;
                }

                // 7. HANDLE FUNCTION.PAUSE()
                if (current.Value == "function" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == "." && _tokens[_position + 2].Value == "pause")
                {
                    _position += 3; // skip function + operator + pause
                    if (_position < _tokens.Count && _tokens[_position].Value == "(") _position++; // skip bracket
                    if (_position < _tokens.Count && _tokens[_position].Value == ")") _position++; // skip bracket

                    _iP = true;
                    _pP = _position;
                    _pausedVariables = new Dictionary<string, object>(_variables);
                    Console.WriteLine("\n[RY System]: Script paused via function.pause()\n");
                    continue;
                }

                // 8. HANDLE FUNCTION.RESUME()
                if (current.Value == "function" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == "." && _tokens[_position + 2].Value == "resume")
                {
                    _position += 3; // skip function + operator + resume
                    if (_position < _tokens.Count && _tokens[_position].Value == "(") _position++; // skip bracket
                    if (_position < _tokens.Count && _tokens[_position].Value == ")") _position++; // skip bracket

                    _iP = false;
                    if (_pP != -1)
                    {
                        _position = _pP;
                    }
                    Console.WriteLine("\n[RY System]: Script resumed via function.resume()\n");
                    continue;
                }

                // 9. HANDLE USER FUNCTION DEFINITIONS (do *functionName*() ... stop)
                if (current.Value == "do" && _position + 1 < _tokens.Count && _tokens[_position + 1].Type == TokenType.Identifier && _position + 2 < _tokens.Count && _tokens[_position + 2].Value == "(")
                {   
                    SkipBlock(); // skip over definition
                    continue; // during top-down execution
                }

                // 9.1. HANDLE STANDALONE NAMESPACE
                if ((current.Value == "math" || current.Value == "sys" || current.Value == "b" || current.Value == "http") && 
                _position + 1 < _tokens.Count && _tokens[_position + 1].Value == ".")
                {
                    EvaluateExpression();
                    continue;
                }

                // 10. HANDLE USER FUNCTION CALLS (functionName())
                if (current.Type == TokenType.Identifier && _position + 1 < _tokens.Count && _tokens[_position + 1].Value == "(")
                {
                    string funcName = current.Value;

                    if (funcName == "print")
                    {
                        _position++;
                        continue;
                    }

                    if (_userFunctions.ContainsKey(funcName))
                    {
                        int returnAddress = _position + 3; // skip identifier, bracket
                        _callStack.Push(returnAddress);
                        _position = _userFunctions[funcName]; // jump to function definition
                    }
                    else
                    {
                        throw new Exception($"Runtime Error: Undefined function '{funcName}'");
                    }
                    continue;
                }

                if (current.Type == TokenType.Keyword && current.Value == "else")
                {
                    _position++;
                    if (_position < _tokens.Count && _tokens[_position].Value == "do")
                    {
                        _position++;
                    }
                    continue;
                }

                if (current.Type == TokenType.Keyword && current.Value == "stop")
                {
                    _position++; // skip 'stop' marker
                    
                    if (_callStack.Count > 0)
                    {
                        _position = _callStack.Pop();
                    }
                    continue;
                }

                // 11. HANDLE COMMENTS
                if (current.Type == TokenType.Comment)
                {
                    _position++;
                    continue;
                }

                _position++;
            }
        }

        private object EvaluateExpression()
        {
            if (_position >= _tokens.Count) return 0;

            Token current = _tokens[_position];

            // check for b namespace
            if (current.Value == "b" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == ".") 
            {
                string bFunc = _tokens[_position + 2].Value;
                _position += 3;
                return _BModule.Execute(bFunc);
            }

            // check for http namespace
            if (current.Value == "http" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == ".")
            {
                string httpFunc = _tokens[_position + 2].Value;
                _position += 3;

                string url = string.Empty;
                string data = string.Empty;

                if (_position < _tokens.Count && _tokens[_position].Value == "(")
                {
                    _position++; // skip bracket
                    url = EvaluateExpression()?.ToString() ?? string.Empty;

                    if (_position < _tokens.Count && _tokens[_position].Value == ",")
                    {
                        _position++; // skip ','
                        data = EvaluateExpression()?.ToString() ?? string.Empty;
                    }

                    if (_position < _tokens.Count && _tokens[_position].Value == ")")
                    {
                        _position++; // skip bracket
                    }
                }

                return _httpModule.Execute(httpFunc, url, data);
            } 

            // check for math namespace
            if (current.Value == "math" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == ".")
            {
                string mathFunc = _tokens[_position + 2].Value;
                _position += 3; 

                object arg = 0;
                if (_position < _tokens.Count && _tokens[_position].Value == "(")
                {
                    _position++; // skip bracket
                    if (_position < _tokens.Count && _tokens[_position].Value != ")")
                    {
                        arg = EvaluateExpression();
                    }
                    if (_position < _tokens.Count && _tokens[_position].Value == ")")
                    {
                        _position++; // skip bracket
                    }
                }

                return _mathModule.Execute(mathFunc, arg);
            }

            // check for sys namespace
            if (current.Value == "sys" && _position + 2 < _tokens.Count && _tokens[_position + 1].Value == ".")
            {
                string sysFunc = _tokens[_position + 2].Value;
                _position += 3; 

                if (_position < _tokens.Count && _tokens[_position].Value == "(")
                {
                    _position++; // skip bracket
                    if (_position < _tokens.Count && _tokens[_position].Value == ")")
                    {
                        _position++; // skip bracket
                    }
                }

                return _sysModule.Execute(sysFunc);
            }

            object leftVal = ResolveValue(current);
            _position++;

            while (_position < _tokens.Count)
            {
                string op = _tokens[_position].Value;
                if (op != "+" && op != "-" && op != "*" && op != "/") break;

                _position++; // skip the operator
                object rightVal = EvaluateExpression(); // evaluate the right side

                leftVal = ComputeBinaryOp(leftVal, op, rightVal);
            }

            return leftVal;
        }

        private object ComputeBinaryOp(object left, string op, object right)
        {
            double l = Convert.ToDouble(left);
            double r = Convert.ToDouble(right);

            double result = op switch
            {
                "+" => l + r,
                "-" => l - r,
                "*" => l * r,
                "/" => r != 0 ? l / r : throw new Exception("Runtime Error: Division by zero"),
                _ => throw new Exception($"Runtime Error: Unknown operator '{op}'")
            };

            if (left is int && right is int && op != "/")
            {
                return (int)result;
            }
            return result;
        }

        private bool EvaluateCondition(out int tokensConsumed)
        {
            tokensConsumed = 0;
            List<Token> condTokens = new();

            while (_position + tokensConsumed < _tokens.Count)
            {
                Token t = _tokens[_position + tokensConsumed];
                if (t.Value == "do" || t.Value == "elseif" || t.Value == "else")
                {
                    break;
                }
                condTokens.Add(t);
                tokensConsumed++;
            }

            if (condTokens.Count >= 3)
            {
                object leftVal = ResolveValue(condTokens[0]);
                string op = condTokens[1].Value;
                object rightVal = ResolveValue(condTokens[2]);

                bool result = CompareValues(leftVal, op, rightVal);

                int idx = 3;
                while (idx + 3 < condTokens.Count)
                {
                    string logicOp = condTokens[idx].Value;
                    object nextLeft = ResolveValue(condTokens[idx + 1]);
                    string nextOp = condTokens[idx + 2].Value;
                    object nextRight = ResolveValue(condTokens[idx + 3]);

                    bool nextResult = CompareValues(nextLeft, nextOp, nextRight);

                    if (logicOp == "and") result = result && nextResult;
                    else if (logicOp == "or") result = result || nextResult;

                    idx += 4;
                }

                return result;
            }

            return false;
        }

        private object ResolveValue(Token token)
        {
            if (token.Type == TokenType.Identifier)
            {
                if (_variables.ContainsKey(token.Value))
                    return _variables[token.Value];
                return token.Value;
            }
            if (int.TryParse(token.Value, out int intVal))
            {
                return intVal;
            }
            if (double.TryParse(token.Value, out double doubleVal))
            {
                return doubleVal;
            }
            return token.Value;
        }

        private bool CompareValues(object left, string op, object right)
        {
            int lNum = 0, rNum = 0;
            bool lIsNum = int.TryParse(left?.ToString(), out lNum);
            bool rIsNum = int.TryParse(right?.ToString(), out rNum);

            bool lBool = false, rBool = false;
            bool lIsBool = bool.TryParse(left?.ToString(), out lBool);
            bool rIsBool = bool.TryParse(right?.ToString(), out rBool);

            return op switch
            {
                "==" => lIsBool && rIsBool ? lBool == rBool : (lIsNum && rIsNum ? lNum == rNum : left?.ToString() == right?.ToString()),
                "!=" => lIsBool && rIsBool ? lBool != rBool : (lIsNum && rIsNum ? lNum != rNum : left?.ToString() != right?.ToString()),
                ">"  => lIsNum && rIsNum && lNum > rNum,
                "<"  => lIsNum && rIsNum && lNum < rNum,
                ">=" => lIsNum && rIsNum && lNum >= rNum,
                "<=" => lIsNum && rIsNum && lNum <= rNum,
                _ => false
            };
        }

        private void SkipBlock()
        {
            int depth = 1;
            while (_position < _tokens.Count && depth > 0)
            {
                if (_tokens[_position].Value == "do")
                {
                    depth++;
                }
                else if (_tokens[_position].Value == "stop")
                {
                    depth--;
                    if (depth == 0)
                    {
                        _position++; // skip past stop
                        break;
                    }
                }
                _position++;
            }
        }

        private void ScanFunctions()
        {
            _userFunctions.Clear();
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Value == "do" && i + 2 < _tokens.Count && 
                    _tokens[i + 1].Type == TokenType.Identifier && _tokens[i + 2].Value == "(")
                {
                    string funcName = _tokens[i + 1].Value;
                    if (funcName != "print")
                    {
                        _userFunctions[funcName] = i; // save start pos
                    }
                }
            }
        }
    }

    public class HttpModule
    {
        private HttpResponseMessage? _lastResponse = null;
        private string _lastResponseBody = string.Empty;

        

        public string Execute(string funcName, string url, string data = "")
        {
            return funcName switch
            {
                "get" => ExecuteGet(url),
                "send" => ExecuteSend(url, data),
                "put" => ExecutePut(url, data),
                "delete" => ExecuteDelete(url),
                "status" => _lastResponse != null ? ((int)_lastResponse.StatusCode).ToString() : "0",
                "headers" => _lastResponse != null ? string.Join(", ", _lastResponse.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}")) : "",
                "body" => _lastResponseBody,
                "download" => "ExecuteDownload(url, data)",
                _ => throw new Exception($"Runtime Error: Unknown namespace function 'http.{funcName}'")
            };
        }

        private string ExecuteDownload(string url, string savePath)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    byte[] fileBytes = client.GetByteArrayAsync(url).Result;
                    System.IO.File.WriteAllBytes(savePath, fileBytes);
                    return "SUCCESS: Downloaded to " + savePath;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }

        private string ExecuteGet(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    _lastResponse = client.GetAsync(url).Result;
                    _lastResponseBody = _lastResponse.Content.ReadAsStringAsync().Result;
                    return _lastResponseBody;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }

        private string ExecuteSend(string url, string data)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var content = new StringContent(data, Encoding.UTF8, "application/json");
                    _lastResponse = client.PostAsync(url, content).Result;
                    _lastResponseBody = _lastResponse.Content.ReadAsStringAsync().Result;
                    return _lastResponseBody;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }

        private string ExecutePut(string url, string data)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var content = new StringContent(data, Encoding.UTF8, "application/json");
                    _lastResponse = client.PutAsync(url, content).Result;
                    _lastResponseBody = _lastResponse.Content.ReadAsStringAsync().Result;
                    return _lastResponseBody;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }

        private string ExecuteDelete(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    _lastResponse = client.DeleteAsync(url).Result;
                    _lastResponseBody = _lastResponse.Content.ReadAsStringAsync().Result;
                    return _lastResponseBody;
                }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }
    }

    public class MathModule
    {
        public object Execute(string mathFunc, object arg)
        {
            return mathFunc switch
            {
                "pi" => Math.PI,
                "inf" => double.PositiveInfinity,
                "absolute" => Math.Abs(Convert.ToDouble(arg)),
                "negate" => -Convert.ToDouble(arg),
                "round" => Math.Round(Convert.ToDouble(arg)),
                "iseven" => Convert.ToInt64(arg) % 2 == 0,
                "isnumber" => double.TryParse(arg?.ToString(), out _),
                "digits" => Math.Abs(Convert.ToInt64(arg)).ToString().Length,
                _ => throw new Exception($"Runtime Error: Unknown math function 'math.{mathFunc}'")
            };
        }
    }

    public class SysModule
    {
        public object Execute(string sysFunc)
        {
            return sysFunc switch
            {
                "currentTime" => DateTime.Now.ToString("HH:mm:ss"),
                "currentDate" => DateTime.Now.ToString("yyyy-MM-dd"),
                "FOD" => Environment.CurrentDirectory,
                "close" => ExitSystem(),
                _ => throw new Exception($"Runtime Error: Unknown system function 'sys.{sysFunc}'")
            };
        }

        private int ExitSystem()
        {
            Environment.Exit(0);
            return 0;
        }
    }

    public class BModule
    {
        public object Execute(string bFunc)
        {
            return bFunc switch
            {
                "random" => new Random().Next(),
                "Keyboard" => throw new Exception("The keyboard function will be added later, as its still in the works"),
                "Gamepad" => throw new Exception("The gamepad function will be added later, as its still in the works"),
                "Mouse" => throw new Exception("The mouse function will be added later, as its still in the works"),
                _ => throw new Exception($"Runtime Error: Unknown namespace function 'b.{bFunc}'")
            };
        }
    }
}