BinaryFormatter is obsolete because it is insecure and cannot be made secure.
However, the current caching implementation depends on it so I need an alternative method
to calculate memory usage of a class instance.  The following text is from 
https://stackoverflow.com/questions/605621/how-to-get-object-size-in-memory


```
Now, how do we calculate the memory requirement for our cached objects? Well, as most of you would know, Int32 and float are four bytes, double and DateTime 8 bytes, char is actually two bytes (not one byte), and so on. String is a bit more complex, 2*(n+1), where n is the length of the string. For objects, it will depend on their members: just sum up the memory requirement of all its members, remembering all object references are simply 4 byte pointers on a 32 bit box. Now, this is actually not quite true, we have not taken care of the overhead of each object in the heap. I am not sure if you need to be concerned about this, but I suppose, if you will be using lots of small objects, you would have to take the overhead into consideration. Each heap object costs as much as its primitive types, plus four bytes for object references (on a 32 bit machine, although BizTalk runs 32 bit on 64 bit machines as well), plus 4 bytes for the type object pointer, and I think 4 bytes for the sync block index. Why is this additional overhead important? Well, let’s imagine we have a class with two Int32 members; in this case, the memory requirement is 16 bytes and not 8.
```