                            Picadillo
        Perfect Collision Awareness-Dependent Lookup Optimization
        ---------------------------------------------------------

                          Kunal Kandekar 
                       kunalkandekar@gmail.com


"Picadillo is a traditional dish in many Latin American countries and 
the Philippines ... that is similar to hash."
- http://en.wikipedia.org/wiki/Picadillo

INTRODUCTION
------------
The idea is simple: Track collisions of hash values (NOT buckets) in a hashtable
(does not matter if open addressing or closed), and  during lookups (whether for
retrieving or removing) only compare the *hash values* and avoid comparing 
the actual keys *unless* a hash collision is detected. Since a hash value is a 
small, fixed size (usually 4 or 8 bytes), a hash comparison is much faster (on 
the order of a couple of instructions) than an actual key comparison, which 
might involve comparison of variable length strings, or more complex objects.

However, as explained below, it requires "perfect collision awareness," which 
means if unexpected keys are searched, lookups can give *false* results! Thus it
has a somewhat narrow applicability.

DETAILS
-------
The problem here is that we cannot rely on only hash comparisons in the default 
case. If we could get away with only hash comparisons, we would, but collisions 
are an unfortunate reality that we must deal with. There is always a small but 
non-zero chance that a hash collision would occur. The larger the hash size and
better the hash function, the smaller this chance, but never does it reach zero.
If we ignored this chance and relied only only on hash comparisons, everything 
would be fast and hunky-dory… until a hash collision inevitably occurs. And when
it does, depending on the application using it, the failure could be 
catastrophic.

Hence, every lookup necessarily involves at least one key comparison (unless the
corresponding bucket is empty), and may involve many more if bucket collisions
have occurred (e.g. when traversing the bucket chain or linear probing.)

The (micro)optimization here is to replace the key comparison with hash value 
comparisons as often as possible, by:
1) detecting if two hash values are equal (simple: they would necessarily index 
into to the same bucket), and
2) if two or more hash values are equal, checking whether the keys are unequal, 
3) and if two entries with equal hash values but unequal keys are detected, 
4) flagging the corresponding buckets as such. 
5) Then for subsequent lookups, only for keys that hash and index into buckets 
which are so flagged are the actual keys compared.

Steps 1 - 4 can trivially be performed with negligible overhead during insert 
operations, because almost always, inserted keys must be compared with keys of 
every colliding bucket anyway, to handle the case where an existing key is being
replaced instead of a new one being inserted.

The advantage here is that we optimize for the overwhelmingly common case, i.e.
the case where hash values don't collide, by replacing the key comparison with
a much faster hash value comparison.

Note that it is possible (although unlikely) that more than 2 keys could have 
the same hash value. In this case, it is probably better to track the count
of colliding buckets rather than just using binary flag for buckets.

VERY IMPORTANT CAVEAT!!!
------------------------
The MAJOR disadvantage is, this depends on *Perfect Collision Awareness* (PCA).
Meaning, we need to know of all possible collisions between keys that have been
inserted and keys that will be looked up, INCLUDING KEYS THAT HAVE NOT BEEN 
INSERTED BUT WILL BE LOOKED UP. Without PCA, there is a very good chance that
the map may return a value for a key that has not been inserted (or the PCADLO 
hashtable is otherwise unaware of) if a collision occurs between an inserted key
and a lookup key that does not exist in the hashtable. 

This is because we only compare hash values for buckets that have had no 
collisions, if a lookup is made for a key that has NOT been inserted but whose 
hash value DOES COLLIDE with another key that has been inserted, the lookup 
returns a FALSE POSITIVE. Hence this hashmap SHOULD ONLY BE USED in cases where 
LOOKUPS ARE FOR KEYS THAT HAVE ALREADY BEEN INSERTED. Alternatively, there 
should be a mechanism to make the PCADLO hashtable aware of all possible keys
that would be looked up, so that it has Perfect Collision Awareness (such as the
"SetExpectedKeys" method in the Java implementation of PCADLO.) In this case, if
there is a possibility that a lookup may be made for a key that has not been
inserted, the key is inserted in the map preemptively with a "dummy" value 
indicating that it's just a placeholder, and the key actually does not exist in 
the map. If such a method is not provided (such as the C# implementation of 
PCADLODictionary), it is recommended that the program insert dummy values for 
such keys itself.

Nevertheless, this should still be a common enough use case, and the PCADLO
optimization, though very simple, gives significant performance gains in some of
the environments tested, making it worthwhile to consider.

*******

Note any advantage disappears when the keys used are as small as the hash 
values themselves, e.g. integer keys.
There is potentially little improvement to be gained, given that many keys 
differ immediately at the first (or in the first few) characters, and hence the
CPU cost difference may be negigible compared to integer or hash comparison.
However initial tests with Java reveal a consistent 5 - 10% improvement in 
lookup times with 0 - 5% improvement in insert and delete times. Memory overhead
is very low, possibly on the order of a byte (to store collision counts) to a 
bit (to store collision flags.)

Another (minor) disadvantage: the hashval collision count must be updated at 
every insert, and re-evaluated at every remove operation. As such, there is some
small CPU overhead. As collision counts must be tracked, so there is also a 
small memory overhead per bucket.

TEST RESULTS
------------
Preliminary tests comparing a Java implementation of collision-tracking based on
HashMap with java.util.HashMap reveal a 5% - 30% improvement in lookup and 
remove times, with negligible impact or 5% improvement in insert times.
Similar results are achieved on .NET/Mono, comparing a PCADLO version of 
Dictionary with the built-in System.Collections.Dictionary in the Mono standard
library.

NOTE: As with all benchmarks, this should be run on a machine with minimal other
applications and services running, since those wildly throw the results off.

This advantage is consistent across various kinds of keys, including:
1) randomly generated strings of size between 8 and 128 bytes;
2) a dictionary of English words;
3) a list of names;
4) randomly generated strings of 8 - 128 characters with a 4 - 8 character 
   common prefix (to test longer string comparison times.)

For randomly generated keys, colliding strings differ at the first character 
itself with fairly high probability, indicating the key string comparison should
return very fast. Yet the speed advantage persists, indicating that comparing 
integers is still much faster than string comparison. Furthermore, in 
experiments, the larger the key string, the greater the speed improvement.

(A fairly exact probability can be easily determined based on the number of 
alphabets in the keys; keys are alphanumeric with uppercase and lower case 
allowed; hence probability of comparison ending at the first character of the 
key is 1/62 ~= 0.12%. Probability of ending at Nth char is 1/(62^N))

In Java/.NET, this may be due to the two main reasons:
1) Mainly, the cache-miss latency incurred in loading the key object from main
memory for comparison. In Java, the key is stored separately in memory, and the
hashtable bucket only holds a pointer to it. Key comparisons require 
de-referencing the pointer, which is an extra indirection that potentially 
frequently incurs a cache miss and corresponding cache line load from RAM.
2) The implementation of String#equals, which also performs a pointer-
comparison, a string length comparison, and an object type (instanceof) 
comparison before actually comparing the characters.

In C++ (Sparsehash's "dense hashtable"), due to the use of templates, the key 
object is stored within the bucket itself. Hence it is inevitably loaded into 
cache everytime a bucket is referenced during lookups, and hence key-comparison
is very fast as it does not incur an extra cache miss and load. In tests with
this implementation there was no noticeable change in performance. However, the
storage of the hash value and the collision count incur extra memory costs on a
per-bucket basis, which become non-trivial for very large hashtables. For this 
reason, applying PCADLO may be unadvisable for C++.

In the Python C implementation of the dict() built-in type, the bucket again 
stores a pointer to the Python object holding the key object, and hence key
comparisons should incur a cache miss penalty. However, again tests with a 
PCADLO implementation of dict revealed no or minor performance improvements. I'm
not sure why there is no bigger difference; my best guess is either (1) the 
Python interpreter does all the dict operations within the C runtime, which 
incurs less memory overhead; or conversely, (2) it incurs so much overhead that
cache misses for any operation is unavoidable, and all that latency over-whelms
any minor speedups due to PCADLO.

Note that the explanation of cache-latency as the main reason for performance
improvement in Java is based on comparison with the C++ tests.

* Alternate acronyms considered (before I discovered picadillo was "similar to
hash"):
- ICHOR: Insert-Constrained Hash-check Optimized Retrieval in Hashtables
- CTIKL: Collision Tracking Intra-Insert Keyspace Lookup Hashtables
- CAIKL; Collision-Aware Intra-Insert Keyspace Lookup Hashtables
- CTIKL: Collision Tracking Insert-Constrained Keyspace Lookup Hashtables
- PCALO: Perfect Collision Awareness-based Lookup (Micro)Optimization Hashtable
- PCAHLO: Perfect Collision Awareness-based Hash Lookup (Micro)Optimization
Tags: Insert-constrained Lookups (ICL), Intra-insert Keyspace Lookups 
(IKL/IIKL), Collision-Aware (CA), Collision Tracking (CT), Hash-check Only
(HCO/HO), Micro-optimization(MO).

