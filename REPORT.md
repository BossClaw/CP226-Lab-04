1. Performance Data

5 clients, each hitting a 5-second sleep on the server sequential stacks them, threaded overlaps them.

What                  Sequential                                    Threaded
How it works          Client 2 waits for Client 1 to finish, etc.   All 5 clients handled at the same time
Expected Time         5 × 5s = ~25 seconds                          All overlap = ~5 seconds
Actual Measured Time  25.00                                         5.00


Reduction % (theoretical): ((25 - 5) / 25) × 100 = 80% reduction!!


2. Analysis

Code Changes
  threaded_client_data struct     Bundles socket + client ID into one pointer so pthread_create can pass both
  client_thread(void*)            The thread entry point required signature for pthreads; unwraps the struct and calls handle_client
  pthread_create(&tid, NULL,      Spawns a new OS thread for this client; main() returns to accept() client_thread, data) immediately
  pthread_detach(tid)             Tells the OS to auto-clean the thread when it exits no pthread_join needed since we don't care about the return value

So why did the time improve if sleep(5) didn't change?

Sequential blocks the main thread on each client Client 2 can't even knock on the door until Client 1's 5 seconds are done.

Threaded gives every client its own thread. They all sleep(5) at the same time, the OS runs them in parallel, and IRL only 5 seconds pass.
Same total work, just no longer single-file.


Scaling Scenarios
  Scenario          Sequential                  Threaded
  10 requests       10 × 5s = 50 seconds        All overlap = ~5 seconds
  1,000 requests    1,000 × 5s = ~83 mins       Theoretically ~5s


That said, 1,000 threads has real limits:
- Each thread gets its own stack 1,000 of them can eat several GB of RAM fast
- The OS caps simultaneous threads per process (usually a few thousand)
- Scheduling 1,000 threads at once has overhead too to just manage all that
- At scales like that, thread pools of workers pulling from a queue could help with duration.


3. Conclusion

Multithreading is a great fit for I/O-heavy systems, and the ceiling is basically your RAM and CPU, but there's a point when threads alone don't offer the best results.



4. Reflections:

Fun fact - this is the first C I’ve ever written! I've never had to write C for any project, and it’s one of those things that I never had to use and figured ‘yeah, I’ll give it a shot when I retire!’ but here we are!  It's always been interesting, I think looking at the Doom 1993 source code was the first time I was able to comprehend enough of it to follow.  Further tangent, a dev buddy of mine shared a story about how in the early 00s he was doing some kind of intese calc logic with Matricies(?), so he would allocate 3x the memory needed and then put either cached calcs, lookup table in the memory for that one object, and then read from either the 'intended point memory' or that extra spot.  I might be mangling the specifics but I know pointers and allocations are serious stuff, quadrupley so when you have to do your own garbage allocation!

As for reflection on the networking / threading concepts : 
How blissfully unaware I was with Unity C# Coroutines, Javascript / Godot / Python await / async, etc... logic because those aren’t true threading, not separate pieces of code running in parallel, but each processing one frame a data every time until they reach their end.

I did actually use true threads with Unity back in 2018 when I was rebuilding a Twitch Streaming Tool and ( for some reason I forget now ) it needed sockets to connect with the Twitch IRC and get mesgs from the chat room, and thus when the tool ran it would start a thread to always run and listen for those sockets separate from the other logic of the tool ( EG: Anim, sounds, rewards… ) and had to be closed properly before the tool fully shut down.  Thankfully I didn’t have to bother with locks! 

So those threads achieved basically the same thing in the demo code - allowing logic that takes a various amount of time to run parallel and not force each connection / client / irc mesg to wait for the previous to finish.
