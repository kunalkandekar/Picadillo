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
The idea is simple: Track collisions of hash values (*NOT* buckets) in a 
hashtable, and  during lookups, only compare the *hash values* and avoid 
comparing the actual keys *unless* a hash collision has been detected. Since a 
hash value is a small, fixed size (usually 4 or 8 bytes), a hash comparison is 
much faster (on the order of a couple of instructions) than an actual key 
comparison, which might involve comparison of variable length strings, or more 
complex objects. More importantly, and less obviously, avoiding a direct key 
comparison also avoids referencing the key object in memory, which reduces the 
need to load it from memory into the CPU registers (via the CPU caches.) This 
reduces the effective memory used and makes more efficient usage of the memory 
as well as the CPU cache, reducing the number of cache misses and loads.

The idea applies regardless of whether the hashtable uses open addressing or 
closed, and applies to all lookups, either for retrieving or removing.

However, as explained below, it requires "perfect collision awareness," which 
means if unexpected or arbitrary keys are searched, lookups can give *false* 
results! This means that correct lookups are only guaranteed for keys that have
been inserted previously. Thus it has a somewhat narrow applicability compared 
to conventional hashtables, but I can still think of several usecases.

This is an absurdly simple micro-optimization with a pretty big limitation (keys
being lookuped must have been previously inserted, else usage is very unsafe), 
yet in various JIT/VM and interpreted environments, it gives between a 5 and 30%
improvement in lookup times!


DETAILS
-------
The problem here is that we cannot rely on only hash comparisons for lookups in 
the default case. If we could get away with only hash comparisons, we would, but
collisions are an unfortunate and inevitable reality that we must deal with. No
matter how good the hash function, there is always a small but non-zero chance 
that a hash collision will occur. The larger the hash size and better the hash 
function, the smaller this probability, but never does it reach zero.

If we ignored this probability and relied only only on hash comparisons, 
everything would be fast and hunky-dory -- until a hash collision inevitably 
occurs. And when it does, depending on the application using it, the failure 
could be catastrophic.

Hence, in conventional hashtables, every lookup necessarily involves at least 
one key comparison (unless the corresponding bucket is empty), and may involve 
many more if bucket collisions have occurred (e.g. when traversing the bucket 
chain or linear probing.) This cost can add up fast if the keys are custom 
objects or composite objects (such as arrays, lists or other data structures) 
that have complicated or expensive comparison semantics. Significant costs may 
be incurred even for string keys if the strings have long common prefixes and
differ only in a few bytes, necessitating a comparison of the entire string upto
the first byte that differs.

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
of colliding buckets rather than just using binary flag for buckets. This is the
approach I used in the reference implementations.


VERY IMPORTANT CAVEAT!!!
------------------------
The MAJOR disadvantage is, this depends on *Perfect Collision Awareness* (PCA).
Meaning, we need to know of all possible collisions between keys that have been
inserted and keys that will be looked up, *INCLUDING KEYS THAT HAVE NOT BEEN 
INSERTED BUT WILL BE LOOKED UP*. Without PCA, there is a very good chance that
the map may return a value for a key that has not been inserted (or the PCADLO 
hashtable is otherwise unaware of) if a hash collision occurs between an 
inserted key and a lookup key that does not exist in the hashtable. 

The benchmark code for the Java reference implementation has a method 
demonstrating such a case, which is very likely using the default hashing for 
Strings.

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
However initial tests with Java reveal a consistent 10 - 30% improvement in 
lookup times with 0 - 5% improvement in insert and delete times. Memory overhead
is very low, possibly on the order of a byte (to store collision counts) to a 
bit (to store collision flags.)

Another (minor) disadvantage: the hashval collision count must be updated at 
every insert, and re-evaluated at every remove operation. As such, there is some
small CPU overhead. As collision counts must be tracked, so there is also a 
small memory overhead per bucket.


BENCHMARK RESULTS
-----------------
Preliminary tests comparing a Java implementation of collision-tracking based on
HashMap with java.util.HashMap reveal a 5% - 30% improvement in lookup and 
remove times, with negligible impact or 5% improvement in insert times.
Similar results are achieved on .NET/Mono, comparing a PCADLO version of 
Dictionary with the built-in System.Collections.Dictionary in the Mono standard
library; Similar results were also achieved for CPython 3.2.3, where the PCADLO
hashtable was introduced as a custom built-in type and benchmarked against the
dict built-in type.

This advantage is consistent across various kinds of keys, including:
1) randomly generated strings of size between 8 and 128 bytes;
2) a dictionary of English words;
3) a list of names;
4) randomly generated strings of 8 - 128 characters with a 4 - 8 character 
   common prefix (to test longer string comparison times.)

NOTE: As with all benchmarks, this should be run on a machine with minimal other
applications and services running, since those wildly throw the results off.
Furthermore, most benchmarks to measure insert times are swamped by the 
latencies involved in allocating new memory and re-hashing hashtables on resize,
so an accurate measure is not available. But logically it makes sense that there
should be minimal change, since we are only negligible increasing the amount of
work done during inserts.

For randomly generated keys, colliding strings differ at the first character 
itself with fairly high probability, indicating the key string comparison should
return very fast. Yet the speed advantage persists, indicating that comparing 
integers is still much faster than string comparison. Furthermore, in 
experiments, the larger the key string, the greater the speed improvement.

(A fairly accurate probability can be easily determined based on the number of 
alphabets in the keys; keys are alphanumeric with uppercase and lower case 
allowed; hence probability of comparison continuing past the first character 
(i.e. a mismatch not occuring at the first character) of the key is 1/62 = 1.6%.
Probability of comparison continuing upto the Nth char is 1/(62^N)).

In Java/.NET/CPython, this may be due to the two main reasons:
1) Mainly, the cache-miss latency incurred in loading the key object from main
memory for comparison. In Java, .NET and CPython, the key object is stored 
separately from the hashtable bucket object in memory, and the bucket only holds
a pointer to the key. Hence, key comparisons require de-referencing the pointer,
which is an extra indirection that potentially frequently incurs a cache miss 
and corresponding cache line load from RAM.
2) The implementation of String#equals, which typically also performs a pointer-
comparison, a string length comparison, and often an object type (instanceof) 
comparison before actually comparing the characters. Note that in CPython, there
is a special method for unicode string keys (lookdict_unicode) that is 
optimistically used by default, which uses a unicode key comparison method 
(unicode_eq) until the first time a non-unicode key object is detected. However,
this still does not get rid of the object type check (PyUnicode_CheckExact) at
every lookup.

In C++ (Sparsehash's "dense hashtable"), due to the use of templates, the key 
object is stored within the bucket itself. Hence it is inevitably loaded into 
cache everytime a bucket is referenced during lookups, and hence key-comparison
is very fast as it does not incur an extra cache miss and load. In tests with
this implementation there was no noticeable change in performance. However, the
storage of the hash value and the collision count incur extra memory costs on a
per-bucket basis, which become non-trivial for very large hashtables. For this 
reason, applying PCADLO may be unadvisable for C++.

In the CPython C implementation of the dict() built-in type, the bucket again 
stores a pointer to the Python object holding the key object, and hence key
comparisons should incur a cache miss penalty. However, preliminary tests with a
PCADLO implementation of dict revealed only minor performance improvements (on 
the order of 3 - 5%). I was unable to explain why there wasn't a bigger 
difference, until I noticed I was making calls to a random number generator in
each pass of the critical loop being timed, and the processing time of the RNG
dwarfed the time attributable to the retrieve operation. Moving the RNG calls 
out of the timed loop and replacing it with a lookup in a pre-generated list of
random numbers suddenly brought performance numbers in line with those for Java
and .NET.

Note that the hypothesis of cache-latency being the main reason for performance
improvement in Java, .NET and CPython is based on comparison with the C++ tests.
