//By: Kunal Kandekar

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

using PCADLO;

public class DictionaryBenchmark
{
    int iters = 1;
    //int keySize = 32;
    string keyStrat = "random:32";
    string keyPrefix = "";
    int nKeys    = 1000*1000;
    int nInserts = 1000*1000;
    int nLookups = 5000*1000;
    int nRemoves = 500*1000;
    
    float fractionUnsuccessfulLookups = 0.0f;
    float initMapSizeFraction = 0.5f;
    
    int seed = 1234;
    char [] chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    Random random = null;
    //PCADLODictionary.Hasher hashFunc = null;
            
    string genRandomString(Random random, int minLen, int maxLen) {
        int strLength = minLen;
        if(maxLen > minLen) {
            strLength = minLen + random.Next(maxLen - minLen);
        }

        char [] strChars = new char[strLength];
        for(int i = 0; i < strLength; i++) {
            strChars[i] = chars[random.Next(chars.Length)];
        }
        return new string(strChars);
    }
    
    public static string trimLeft(string s) {
        return s.Trim();    //Regex.Replace(s, "^\\s+", "");
    }
    
    public static string trimRight(string s) {
        return Regex.Replace(s, "\\s+$", "");
    } 

    List<string> generateKeys() {
        string strategy = this.keyStrat;
        //long t0 = System.Environment.TickCount;

        //generate random keys
        if(nInserts < nKeys) {
            //otherwise fraction is incorrect
            fractionUnsuccessfulLookups = 0.0f;
        }

        //use "searchspace" larger than actual keyspace to simulate unsuccessful lookups
        int nSearchSpace = (int)(nKeys / (1.0 - fractionUnsuccessfulLookups)); //this is VERY approximate ... too lazy to do the actual math

        List<string> keyList = new List<string>(nSearchSpace);
        
        long t1 = System.Environment.TickCount;
        long totalKeyLen = 0;

        HashSet<string> dupchk = new HashSet<string>();
        if(strategy.StartsWith("random:")) {
            string keySizeSpec = strategy.Substring("random:".Length);
            string [] tokens   = keySizeSpec.Split(new Char[] {'-'});

            int keySizeMin = Convert.ToInt32(tokens[0]);
            int keySizeMax = (tokens.Length > 1 ? Convert.ToInt32(tokens[1]) : keySizeMin);
            for(int i = 0; i < nSearchSpace; i++) {
                string key = keyPrefix + genRandomString(random, keySizeMin, keySizeMax);
                //eliminate dups
                if(!dupchk.Contains(key)) {
                    totalKeyLen += key.Length;
                    keyList.Add(key);
                    dupchk.Add(key);
                }
                else {
                    i--; //compensate for dup
                }
            }
        }
        else if(strategy.StartsWith("file:")) {
            string fname = strategy.Substring("file:".Length);
            try {
                System.IO.StreamReader br = new System.IO.StreamReader(fname);
                string line;
                int count = 0;
                while((line = br.ReadLine()) != null) {
                    line = line.Trim();
                    if(line.Length < 1) {
                        continue;
                    }
                    string [] tokens = line.Split(new Char [] {' '});
                    string key = keyPrefix + tokens[0].Trim();
                    //eliminate dups
                    if(!dupchk.Contains(key)) {
                        totalKeyLen += key.Length;
                        keyList.Add(key);
                        dupchk.Add(key);
                        count++;
                    }
                    if(count > nSearchSpace) {
                        break;
                    }
                }
                nInserts = nKeys = keyList.Count;
            }
            catch(Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
        else if(strategy.StartsWith("wordnetdict:")) {
            string fname = strategy.Substring("wordnetdict:".Length);
            try {
                System.IO.StreamReader br = new System.IO.StreamReader(fname);
                string line;
                int count = 0;
                while((line = br.ReadLine()) != null) {
                    line = line.Trim();
                    if((line.Length < 1) || (!Char.IsLetter(line[0]))) {
                        continue;
                    }
                    string [] tokens = line.Split(new Char [] {' '});
                    string key = keyPrefix + tokens[0].Trim();
                    //eliminate dups
                    if(!dupchk.Contains(key)) {
                        totalKeyLen += key.Length;
                        keyList.Add(key);
                        dupchk.Add(key);
                        count++;
                    }
                    if(count > nSearchSpace) {
                        break;
                    }
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex.ToString());
            }
            nInserts = nKeys = keyList.Count;
        }
        long t2 = System.Environment.TickCount;        
        long tKeyGen = t2 - t1;
        
        Console.WriteLine("Generated "+keyList.Count+" keys ("
                    +(keyList.Count - nKeys)+" extra) of avg length "
                    +(totalKeyLen * 1.0 / keyList.Count)+ " in "+ tKeyGen + " ms"); 
        seed = random.Next();

        return keyList;
    }

    float testMapGrow(string dictType, List<string> keyList, IDictionary<string, string> map) {
        //int nSearchSpace = keyList.Count;
        //verify with stopwatch
    	Stopwatch stopwatch = new Stopwatch();

        stopwatch.Start();
        long t1 = System.Environment.TickCount;
        //NOTE: new string object created for each key to remove advantage of pointer comparison during key comparison
        //having keys with the same pointer is probably not the most common case, especially since keys to lookup
        //often come from external input (e.g. looking up session variables from HTTP request parameters, or looking up
        //parameter values in HTTP request objects using parameter key strings hardcoded in the application logic)

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            string key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList[i];
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList[random.Next(nKeys)];
            }
            map.Add(string.Copy(key), key);
        }
        stopwatch.Stop();
        long t2 = System.Environment.TickCount;
        long tKeyGrow = t2 - t1;
        float avgGrow = ((1000000.0f * tKeyGrow)/nInserts);
        Console.WriteLine(string.Format(dictType+" - Avg Map Grow: {0:000.000} ns, ({1} ={0:000.000} ns)", 
            avgGrow, (Stopwatch.IsHighResolution ? "Hi-res" : "Lo-res"), (stopwatch.ElapsedMilliseconds * 1000000.0f) / nInserts));
        return avgGrow;
    }
    
    public static void demoFailureCase() {
        Console.WriteLine("Demo-ing failure case: false positive for non-inserted key:");
        PCADLO.PCADLODictionary<string, string> map = new PCADLO.PCADLODictionary<string, string>();
        map.Add("y5ymMhdR", "y5ymMhdR");    //note, key "KRHliuFh" is NOT inserted
        string val = null;
        string nonInsertedKey = "KRHliuFh";
        map.TryGetValue(nonInsertedKey, out val);
        Console.WriteLine("getting non-inserted key \""+nonInsertedKey+"\" from map "+ map.ToString()+"\nreturns value: "+val);
        //DictionaryExtensions.PrettyPrint<string, string>(map)
    }
    
    void validateMaps(List<string> keyList, Dictionary<string, string> map1, PCADLO.PCADLODictionary<string, string> map2, string validateMethod) {
        int nSearchSpace = keyList.Count;
        if(nInserts < nSearchSpace) {
            nInserts = nSearchSpace;
        }
        //reset seed for repeatibility between iterations
        random = new Random(seed);
        Console.WriteLine("Starting...");
        //long t1 = System.Environment.TickCount;
        bool validated = true;
        
        bool expKeys = validateMethod.Equals("valid8-expkeys");
        if(expKeys) {
            //map2.setExpectedKeys(keyList);
        }

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            string key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList[i];
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList[random.Next(nKeys)];
            }
            string k = string.Copy(key); 
            map1.Add(k, key);
            map2.Add(k, key);
            if(!expKeys && (map1.Count != map2.Count)) {
                Console.WriteLine("Inconsistency at put("+k+"): "+map1.Count+" != "+ map2.Count);
                validated = false;
                //System.exit(1);
            }
        }
        
        for(int i = 0; i < nLookups; i++) {
            string key = keyList[random.Next(nSearchSpace)];
            string val1 = null;
            string val2 = null;
            map1.TryGetValue(key, out val1);
            map2.TryGetValue(key, out val2);
            if(val1 != val2) {
                Console.WriteLine("Inconsistency at get("+key+"[hash:"+EqualityComparer<string>.Default.GetHashCode(key)+"]): "+val1+"[hash:"+EqualityComparer<string>.Default.GetHashCode(val1)+"] != "+ val2+"[hash:"+EqualityComparer<string>.Default.GetHashCode(val2)+"]");
                //Console.WriteLine("hash["+key+"]="+key.hashCode()+" hash["+val1+"]="+(val1 != null ? val1.hashCode() : null)+" hash["+val2+"]="+(val2 != null ? val2.hashCode() : null));
                validated = false;
                //System.exit(1);
            }
        }
        if(nRemoves >= nSearchSpace) {
            nRemoves = nSearchSpace;
        }
        for(int i = 0; i < nRemoves; i++) {
            string key = keyList[i];
            bool val1 = map1.Remove(key);
            bool val2 = map2.Remove(key);
            if(val1 != val2) {
                Console.WriteLine("Inconsistency at remove("+key+"): "+val1+" != "+ val2);
                validated = false;
                //System.exit(1);
            }
        }
        if(validated) {
            Console.WriteLine("Map operation validated");
        }
        else {
            Console.WriteLine("Map operation not validated");
        }
        Environment.Exit(0);
    }
    
    float [] testMap(List<string> keyList, IDictionary<string, string> map, out long checksum) {
        int nSearchSpace = keyList.Count;
        if(nInserts < nSearchSpace) {
            nInserts = nSearchSpace;
        }
        //reset seed for repeatibility between iterations
        random = new Random(seed);

        long t2 = System.Environment.TickCount;
        //NOTE: new string object created for each key to remove advantage of pointer comparison during key comparison
        //having keys with the same pointer is probably not the most common case, especially since keys to lookup
        //often come from external input (e.g. looking up session variables from HTTP request parameters, or looking up
        //parameter values in HTTP request objects using parameter key strings hardcoded in the application logic)

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            string key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList[i];
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList[random.Next(nKeys)];
            }
            map.Add(string.Copy(key), key);
        }

        long t3 = System.Environment.TickCount;
        long tKeyInsert = t3 - t2;
        
        //generate random lookups
        long successfulLookups = 0; //dummy variable to avoid optimizing non-side-effect code
        checksum = 0;  //sum of all val hashcodes

        for(int i = 0; i < nLookups; i++) {
            string key = keyList[random.Next(nSearchSpace)];
            string val = null;
            map.TryGetValue(key, out val);
            if(val != null) {
                checksum ^= val.GetHashCode();
                successfulLookups++;
            } 
        }
        long t4 = System.Environment.TickCount;
        long tKeyLookup = t4 - t3;
        
        long successfulRemoves = 0; //dummy variable to avoid optimizing non-side-effect code
        if(nRemoves >= nSearchSpace) {
            nRemoves = nSearchSpace;
        }
        for(int i = 0; i < nRemoves; i++) {
            string key = keyList[i];
            bool val = map.Remove(key);
            if(val) {
               successfulRemoves++;
            } 
        }

        long t5 = System.Environment.TickCount;
        long tKeyRemove = t5 - t4;
        
        Console.WriteLine("Checksum = "+checksum+", Successful lookups = "+(successfulLookups * 100.0 / nLookups)+"%, successful removes = "+(successfulRemoves * 100.0 / nRemoves)+"%");
        if(map is PCADLO.PCADLODictionary<string,string>) {
            Console.WriteLine("Num hash collisions = "+ ((PCADLO.PCADLODictionary<string,string>)map).NumHashCollisions);
        }
        Console.WriteLine(string.Format("Total Insert: {0:} ms, Lookup: {1:} ms, Remove {2:} ms", tKeyInsert, tKeyLookup, tKeyRemove));
        float avgInsert = ((1000000.0f * tKeyInsert)/nInserts);
        float avgLookup = ((1000000.0f * tKeyLookup)/nLookups);
        float avgRemove = ((1000000.0f * tKeyRemove)/nRemoves);
        Console.WriteLine(string.Format("Avg Insert: {0:000.000} ns, Lookup: {1:000.000} ns, Remove {2:000.000} ns", avgInsert, avgLookup, avgRemove));
        return new float[] {avgInsert, avgLookup, avgRemove};
    }
    
    float[] testPlainDictionary(List<string> keyList, out long checksum) {
        checksum = 0;
        return testMap(keyList, new Dictionary<string, string>((int)(nInserts * initMapSizeFraction)), out checksum);
    }
    
    float [] testPCADLODictionary(List<string> keyList, out long checksum) {
        checksum = 0;
        return testMap(keyList, new PCADLO.PCADLODictionary<string, string>((int)(nInserts * initMapSizeFraction)), out checksum);
    }
    
    public void init() {
        random = new Random(seed);
    }
    
    //On *nix/mono: compile using: gmcs DictionaryBenchmark.cs PCADLODictionary.cs
    static void Main(string[] args) {
        if(args.Length < 1) {
            Console.WriteLine("mono DictionaryBenchmark -k <key-strat> -p <key-prefix> "
            +"-i <num-inserts> -l <num-lookups> -r <num-removes> -s <seed> -f <failed-lookup-fraction> "
            +"--dump <what-to-dump>");
            return;
        }
        
        //Dictionary<string, PCADLODictionary.Hasher> hashers = new Dictionary<string, PCADLODictionary.Hasher>();
        //hashers.Add("default", PCADLODictionary.defaultHasher);
        
        int iters = 1;
        int seed = 0;
        string validate = null;
        DictionaryBenchmark hmb = new DictionaryBenchmark();
        for(int i = 0; i < args.Length; i+=2) {
            if(i >= (args.Length - 1)) {
                break;
            }
            if(args[i].Equals("-k")) {
                hmb.keyStrat = args[i + 1];
            }
            if(args[i].Equals("-p")) {
                hmb.keyPrefix = args[i + 1];
            }
            else if(args[i].Equals("-n")) {
                hmb.nKeys = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("-i")) {
                hmb.nInserts = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("-l")) {
                hmb.nLookups = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("-r")) {
                hmb.nRemoves = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("-s")) {
                hmb.seed = seed = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("--reps")) {
                iters = Convert.ToInt32(args[i + 1]);
            }
            else if(args[i].Equals("--init")) {
                hmb.initMapSizeFraction = (float)Convert.ToDouble(args[i + 1]);
            }
            else if(args[i].Equals("-f")) {
                hmb.fractionUnsuccessfulLookups = (float)Convert.ToDouble(args[i + 1]);
            }
            else if(args[i].Equals("-h")) {
                //hmb.hashFunc = hashers.get(args[i + 1]);
            }
            else if(args[i].Equals("--dump")) {
                if(args[i + 1].StartsWith("col")) {
                    //PCADLO.PCADLODictionary.dumpHashCollisions = true;
                }
                if(args[i + 1].StartsWith("valid8")) {
                    validate = args[i + 1];
                }
            }
            
        }
        Console.WriteLine("Generating keys...");
        hmb.init();
        if(seed < 0) {
            hmb.seed = System.Environment.TickCount;
            Console.WriteLine("Temp seed = "+hmb.seed);
        }
        List<string> keyList = hmb.generateKeys();
        demoFailureCase();
        if(validate != null) {
            hmb.validateMaps(keyList, new Dictionary<string,string>(), new PCADLO.PCADLODictionary<string, string>(), validate);
        }
        hmb.testMapGrow("Dictionary", keyList, new Dictionary<string, string>());
        hmb.testMapGrow("PCADLO Dictionary", keyList, new PCADLO.PCADLODictionary<string, string>());
        
        for(int i = 0; i < iters; i++) {
            Console.WriteLine("\n------\nIteration #"+(i + 1));
            //randomize order of tests to check if running one before the other affects results
            hmb.random = new Random(System.Environment.TickCount);
            float [] avglatPlain = null;
            float [] avglatPCADLO = null;
            long checksumPlain = 0;
            long checksumPCADLO = 0;
            if(seed < 0) {
                hmb.seed = System.Environment.TickCount;
                Console.WriteLine("Temp seed = "+hmb.seed);
            }
            if(hmb.random.Next() % 2 == 0) {
                Console.WriteLine("\nPlain Dictionary");
                avglatPlain = hmb.testPlainDictionary(keyList, out checksumPlain);
                Console.WriteLine("\nPCADLO Dictionary");
                avglatPCADLO = hmb.testPCADLODictionary(keyList, out checksumPCADLO);
            }
            else {
                Console.WriteLine("\nPCADLO Dictionary");
                avglatPCADLO = hmb.testPCADLODictionary(keyList, out checksumPCADLO);
                Console.WriteLine("\nPlain Dictionary");
                avglatPlain = hmb.testPlainDictionary(keyList, out checksumPlain);
            }
            if(checksumPCADLO != checksumPlain) {
                Console.WriteLine("\nChecksum mismatch: Hash-collision detected!!!!");
            }
            Console.WriteLine("\nPCADLO : Plain % improvement: ");
            Console.WriteLine(string.Format("Avg Insert: {0:00.000}%, Lookup: {1:00.000}%, Remove {2:00.000}%", 
                (100.0f * (avglatPlain[0] - avglatPCADLO[0])/avglatPlain[0]), 
                (100.0f * (avglatPlain[1] - avglatPCADLO[1])/avglatPlain[1]),
                (100.0f * (avglatPlain[2] - avglatPCADLO[2])/avglatPlain[2])));
        }
    }
    
}
