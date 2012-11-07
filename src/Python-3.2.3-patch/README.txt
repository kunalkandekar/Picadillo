Picadillo patch for CPython 3.2.3

This patch introduces a PCADLO hashtable as a built-in type (pcadlo) to the
CPython 3.2.3 runtime. Usage of pcadlo is identical to usage of dict. After all
the pcadlo object is simply an adapted version of the dict built-in type (found 
in $Python-3.2.3/Include/dictobject.h and $Python-3.2.3/Objects/dictobject.c) 

NB: The caveat of only looking up keys you have inserted that applies to the
Picadillo scheme still applies here.

Applying the patch
------------------
1. Download and extract the CPython 3.2.3 tarball.
2. Copy the cpython-3.2.3-pcadlo.patch file into the Python-3.2.3 root directory
3. Apply the patch by running the following command:
    patch -p1 < cpython-3.2.3-pcadlo.patch
4. Do the configure/make routine:
    ./configure
    ./make

To revert the patch, type:
    patch -p1 -R < cpython-3.2.3-pcadlo.patch

NB: You might not want to do 'make install', though, as this is only an 
experimental patch, and it may not be wise to use this to replace whatever 
version of Python you are using. This means to test the patch, you will have to
use the python.exe executable generated in the root directoy. (Yes, it makes a 
.exe even on *nix.)

Testing the pcadlo type
-----------------------
./python.exe
>>> dict()
{}
>>> pcadlo()
{}
>>> p = pcadlo()
>>> p['one'] = 1
>>> p[2] = 'two'
>>> p
{2: 'two', 'one': 1}
>>> 

Running the benchmarks
-----------------------
See the testpcadlo.py file in the Python-3.2.3 root directory. Usage is:
    ./python.exe testpcadlo.exe <rng-seed> <num-keys> <num-reps> <test-op> <key-type> <key-gen>


For details, type:
    ./python.exe ./testpcadlo.py -h
or
    ./python.exe ./testpcadlo.py --help
    
Example usage
    ./python.exe ./testpcadlo.py 2321 10K 1K get str rng
