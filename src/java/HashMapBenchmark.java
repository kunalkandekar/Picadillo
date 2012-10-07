//By: Kunal Kandekar

import java.util.*;
import java.io.*;

public class HashMapBenchmark {
    int iters = 1;
    //int keySize = 32;
    String keyStrat = "random:32";
    String keyPrefix = "";
    int nKeys    = 1000*1000;
    int nInserts = 1000*1000;
    int nLookups = 5000*1000;
    int nRemoves = 500*1000;
    
    float fractionUnsuccessfulLookups = 0.0f;
    float initMapSizeFraction = 0.5f;
    
    long seed = 1234;
    final char [] chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".toCharArray();
    Random random = null;
    PCADLOMap.Hasher hashFunc = null;
            
    String genRandomString(Random random, int minLen, int maxLen) {
        int strLength = minLen;
        if(maxLen > minLen) {
            strLength = minLen + random.nextInt(maxLen - minLen);
        }

        char [] strChars = new char[strLength];
        for(int i = 0; i < strLength; i++) {
            strChars[i] = chars[random.nextInt(chars.length)];
        }
        return new String(strChars);
    }
    
    public static String trimLeft(String s) {
        return s.replaceAll("^\\s+", "");
    }
    
    public static String trimRight(String s) {
        return s.replaceAll("\\s+$", "");
    } 

    List<String> generateKeys() {
        String strategy = this.keyStrat;
        long t0 = System.currentTimeMillis();

        //generate random keys
        if(nInserts < nKeys) {
            //otherwise fraction is incorrect
            fractionUnsuccessfulLookups = 0.0f;
        }

        //use "searchspace" larger than actual keyspace to simulate unsuccessful lookups
        int nSearchSpace = (int)(nKeys / (1.0 - fractionUnsuccessfulLookups)); //this is VERY approximate ... too lazy to do the actual math

        List<String> keyList = new ArrayList<String>(nSearchSpace);
        
        long t1 = System.currentTimeMillis();
        long totalKeyLen = 0;
        
        if(strategy.startsWith("random:")) {
            String keySizeSpec = strategy.substring("random:".length());
            String [] tokens   = keySizeSpec.split("-");

            int keySizeMin = Integer.decode(tokens[0]);
            int keySizeMax = (tokens.length > 1 ? Integer.decode(tokens[1]) : keySizeMin);
            for(int i = 0; i < nSearchSpace; i++) {
                String key = keyPrefix + genRandomString(random, keySizeMin, keySizeMax);
                totalKeyLen += key.length();
                keyList.add(key);
            }
        }
        else if(strategy.startsWith("file:")) {
            String fname = strategy.substring("file:".length());
            try {
                BufferedReader br = new BufferedReader(new FileReader(new File(fname)));
                String line;
                int count = 0;
                while((line = br.readLine()) != null) {
                    line = trimLeft(line.trim());
                    if(line.length() < 1) {
                        continue;
                    }
                    String [] tokens = line.split(" ");
                    String key = keyPrefix + tokens[0].trim();
                    totalKeyLen += key.length();
                    keyList.add(key);
                    count++;
                    if(count > nSearchSpace) {
                        break;
                    }
                }
                nKeys = keyList.size();
            }
            catch(Exception ex) {
                ex.printStackTrace();
            }
        }
        else if(strategy.startsWith("wordnetdict:")) {
            String fname = strategy.substring("wordnetdict:".length());
            try {
                BufferedReader br = new BufferedReader(new FileReader(new File(fname)));
                String line;
                int count = 0;
                while((line = br.readLine()) != null) {
                    line = trimLeft(line.trim());
                    if((line.length() < 1) || (!Character.isLetter(line.charAt(0)))) {
                        continue;
                    }
                    String [] tokens = line.split(" ");
                    String key = keyPrefix + tokens[0].trim();
                    totalKeyLen += key.length();
                    keyList.add(key);
                    count++;
                    if(count > nSearchSpace) {
                        break;
                    }
                }
            }
            catch(Exception ex) {
                ex.printStackTrace();
            }
            nKeys = keyList.size();
        }
        long t2 = System.currentTimeMillis();        
        long tKeyGen = t2 - t1;
        
        System.out.println("Generated "+keyList.size()+" keys ("
                    +(keyList.size() - nKeys)+" extra) of avg length "
                    +(totalKeyLen * 1.0 / keyList.size())+ " in "+ tKeyGen + " ms"); 
        seed = random.nextLong();

        return keyList;
    }

    float testMapGrow(List<String> keyList, Map<String, String> map) {
        int nSearchSpace = keyList.size();
        long t1 = System.currentTimeMillis();
        //NOTE: new string object created for each key to remove advantage of pointer comparison during key comparison
        //having keys with the same pointer is probably not the most common case, especially since keys to lookup
        //often come from external input (e.g. looking up session variables from HTTP request parameters, or looking up
        //parameter values in HTTP request objects using parameter key strings hardcoded in the application logic)

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            String key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList.get(i);
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList.get(random.nextInt(nKeys));
            }
            map.put(new String(key), key);
        }

        long t2 = System.currentTimeMillis();
        long tKeyGrow = t2 - t1;
        float avgGrow = ((1000000.0f * tKeyGrow)/nInserts);
        System.out.println(String.format("Avg Map Grow: %3.3f ns", avgGrow));
        return avgGrow;
    }
    
    public static void demoFailureCase() {
        System.out.println("Demo-ing failure case: false positive for non-inserted key:");
        PCADLOMap<String, String> map = new PCADLOMap<String, String>();
        map.put("y5ymMhdR", "y5ymMhdR");    //note, key "KRHliuFh" is NOT inserted
        System.out.println("getting non-inserted key \"KRHliuFh\" from map "+ map+"\nreturns value: "+map.get("KRHliuFh"));
    }
    
    void validateMaps(List<String> keyList, HashMap<String, String> map1, PCADLOMap<String, String> map2, String validateMethod) {
        int nSearchSpace = keyList.size();
        //reset seed for repeatibility between iterations
        random.setSeed(seed);

        long t1 = System.currentTimeMillis();
        boolean validated = true;
        
        boolean expKeys = validateMethod.equals("valid8-expkeys");
        if(expKeys) {
            map2.setExpectedKeys(keyList);
        }

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            String key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList.get(i);
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList.get(random.nextInt(nKeys));
            }
            String k = new String(key); 
            map1.put(k, key);
            map2.put(k, key);
            if(!expKeys && (map1.size() != map2.size())) {
                System.out.println("Inconsistency at put("+k+"): "+map1.size()+" != "+ map2.size());
                validated = false;
                //System.exit(1);
            }
        }
        
        for(int i = 0; i < nLookups; i++) {
            String key = keyList.get(random.nextInt(nSearchSpace));
            String val1 = map1.get(key);
            String val2 = map2.get(key);
            if(val1 != val2) {
                System.out.println("Inconsistency at get("+key+"): "+val1+" != "+ val2);
                //System.out.println("hash["+key+"]="+key.hashCode()+" hash["+val1+"]="+(val1 != null ? val1.hashCode() : null)+" hash["+val2+"]="+(val2 != null ? val2.hashCode() : null));
                validated = false;
                //System.exit(1);
            }
        }
        if(nRemoves >= nSearchSpace) {
            nRemoves = nSearchSpace;
        }
        for(int i = 0; i < nRemoves; i++) {
            String key = keyList.get(i);
            String val1 = map1.remove(key);
            String val2 = map2.remove(key);
            if(val1 != val2) {
                System.out.println("Inconsistency at remove("+key+"): "+val1+" != "+ val2);
                validated = false;
                //System.exit(1);
            }
        }
        if(validated) {
            System.out.println("Map operation validated");
        }
        else {
            System.out.println("Map operation not validated");
        }
        System.exit(0);
        
    }
    
    float [] testMap(List<String> keyList, Map<String, String> map) {
        int nSearchSpace = keyList.size();
        //reset seed for repeatibility between iterations
        random.setSeed(seed);

        long t2 = System.currentTimeMillis();
        //NOTE: new string object created for each key to remove advantage of pointer comparison during key comparison
        //having keys with the same pointer is probably not the most common case, especially since keys to lookup
        //often come from external input (e.g. looking up session variables from HTTP request parameters, or looking up
        //parameter values in HTTP request objects using parameter key strings hardcoded in the application logic)

        //else insert randomly (overwriting inserts ok)
        for(int i = 0; i < nInserts; i++) {
            String key = null;
            if(i < nKeys) {
                //if more keys than nInserts, insert them all
                key = keyList.get(i);
            }
            else {
                //else re-insert previously inserted one (test for collisions while overwriting)
                key = keyList.get(random.nextInt(nKeys));
            }
            map.put(new String(key), key);
        }

        long t3 = System.currentTimeMillis();
        long tKeyInsert = t3 - t2;
        
        //generate random lookups
        long successfulLookups = 0; //dummy variable to avoid optimizing non-side-effect code
        long checksum = 0;  //sum of all val hashcodes

        for(int i = 0; i < nLookups; i++) {
            String key = keyList.get(random.nextInt(nSearchSpace));
            String val = map.get(key);
            if(val != null) {
                checksum ^= val.hashCode();
                successfulLookups++;
            } 
        }
        long t4 = System.currentTimeMillis();
        long tKeyLookup = t4 - t3;
        
        long successfulRemoves = 0; //dummy variable to avoid optimizing non-side-effect code
        if(nRemoves >= nSearchSpace) {
            nRemoves = nSearchSpace;
        }
        for(int i = 0; i < nRemoves; i++) {
            String key = keyList.get(i);
            String val = map.remove(key);
            if(val != null) {
               successfulRemoves++;
            } 
        }

        long t5 = System.currentTimeMillis();
        long tKeyRemove = t5 - t4;
        
        System.out.println("Checksum = "+checksum+", Successful lookups = "+(successfulLookups * 100.0 / nLookups)+"%, successful removes = "+(successfulRemoves * 100.0 / nRemoves)+"%");
        if(map instanceof PCADLOMap) {
            System.out.println("Num hash collisions = "+ ((PCADLOMap)map).nHashCollisions);
        }
        System.out.println(String.format("Total Insert: %5d ms, Lookup: %5d ms, Remove %5d ms", tKeyInsert, tKeyLookup, tKeyRemove));
        float avgInsert = ((1000000.0f * tKeyInsert)/nInserts);
        float avgLookup = ((1000000.0f * tKeyLookup)/nLookups);
        float avgRemove = ((1000000.0f * tKeyRemove)/nRemoves);
        System.out.println(String.format("Avg Insert: %3.3f ns, Lookup: %3.3f ns, Remove %3.3f ns", avgInsert, avgLookup, avgRemove));
        return new float[] {avgInsert, avgLookup, avgRemove};
    }
    
    float[] testPlainHashMap(List<String> keyList) {
        testMapGrow(keyList, new HashMap<String, String>());
        return testMap(keyList, new HashMap<String, String>((int)(nInserts * initMapSizeFraction)));
    }
    
    float [] testPCADLOMap(List<String> keyList) {
        testMapGrow(keyList, new PCADLOMap<String, String>());
        return testMap(keyList, new PCADLOMap<String, String>((int)(nInserts * initMapSizeFraction)));
    }
    
    public void init() {
        random = new Random(seed);
    }
    
    //compile using javac HashMapBenchmark.java
    public static void main(String [] args) {
        if(args.length < 1) {
            System.out.println("java HashMapBenchmark -k <key-strat> -p <key-prefix> "
            +"-i <num-inserts> -l <num-lookups> -r <num-removes> -s <seed> -f <failed-lookup-fraction> "
            +"--dump <what-to-dump>");
            System.exit(1);
        }
        
        Map<String, PCADLOMap.Hasher> hashers = new HashMap<String, PCADLOMap.Hasher>();
        hashers.put("default", PCADLOMap.defaultHasher);
        
        int iters = 1;
        long seed = 0;
        String validate = null;
        HashMapBenchmark hmb = new HashMapBenchmark();
        for(int i = 0; i < args.length; i+=2) {
            if(i >= (args.length - 1)) {
                break;
            }
            if(args[i].equals("-k")) {
                hmb.keyStrat = args[i + 1];
            }
            if(args[i].equals("-p")) {
                hmb.keyPrefix = args[i + 1];
            }
            else if(args[i].equals("-n")) {
                hmb.nKeys = Integer.decode(args[i + 1]);
            }
            else if(args[i].equals("-i")) {
                hmb.nInserts = Integer.decode(args[i + 1]);
            }
            else if(args[i].equals("-l")) {
                hmb.nLookups = Integer.decode(args[i + 1]);
            }
            else if(args[i].equals("-r")) {
                hmb.nRemoves = Integer.decode(args[i + 1]);
            }
            else if(args[i].equals("-s")) {
                hmb.seed = seed = Long.decode(args[i + 1]);
            }
            else if(args[i].equals("--reps")) {
                iters = Integer.decode(args[i + 1]);
            }
            else if(args[i].equals("--init")) {
                hmb.initMapSizeFraction = Float.parseFloat(args[i + 1]);
            }
            else if(args[i].equals("-f")) {
                hmb.fractionUnsuccessfulLookups = Float.parseFloat(args[i + 1]);
            }
            else if(args[i].equals("-h")) {
                hmb.hashFunc = hashers.get(args[i + 1]);
            }
            else if(args[i].equals("--dump")) {
                if(args[i + 1].startsWith("col")) {
                    PCADLOMap.dumpHashCollisions = true;
                }
                if(args[i + 1].startsWith("valid8")) {
                    validate = args[i + 1];
                }
            }
            
        }
        System.out.println("Generating keys...");
        hmb.init();
        if(seed < 0) {
            hmb.seed = System.currentTimeMillis();
            System.out.println("Temp seed = "+hmb.seed);
        }
        List<String> keyList = hmb.generateKeys();
        demoFailureCase();
        if(validate != null) {
            hmb.validateMaps(keyList, new HashMap<String,String>(), new PCADLOMap<String, String>(), validate);
        }
        
        for(int i = 0; i < iters; i++) {
            System.out.println("\n------\nIteration #"+(i + 1));
            //randomize order of tests to check if running one before the other affects results
            hmb.random.setSeed(System.currentTimeMillis());
            float [] avglatPlain = null;
            float [] avglatPCADL = null;
            if(seed < 0) {
                hmb.seed = System.currentTimeMillis();
                System.out.println("Temp seed = "+hmb.seed);
            }
            if(hmb.random.nextInt() % 2 == 0) {
                System.out.println("\nPlain hashmap");
                avglatPlain = hmb.testPlainHashMap(keyList);
                System.out.println("\nPCADLO hashmap");
                avglatPCADL = hmb.testPCADLOMap(keyList);
            }
            else {
                System.out.println("\nPCADLO hashmap");
                avglatPCADL = hmb.testPCADLOMap(keyList);
                System.out.println("\nPlain hashmap");
                avglatPlain = hmb.testPlainHashMap(keyList);
            }
            System.out.println("\nPCADLO : Plain % improvement: ");
            System.out.println(String.format("Avg Insert: %2.3f%%, Lookup: %2.3f%%, Remove %2.3f%%", 
                (100.0f * (avglatPlain[0] - avglatPCADL[0])/avglatPlain[0]), 
                (100.0f * (avglatPlain[1] - avglatPCADL[1])/avglatPlain[1]),
                (100.0f * (avglatPlain[2] - avglatPCADL[2])/avglatPlain[2])));
        }
    }
    
}
