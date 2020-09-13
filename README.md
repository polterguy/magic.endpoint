
# Magic Endpoints

[![Build status](https://travis-ci.org/polterguy/magic.endpoint.svg?master)](https://travis-ci.org/polterguy/magic.endpoint)

Magic Endpoints is a dynamic endpoint URL controller, allowing you to declare endpoints that are dynamically
resolved using your `IExecutorAsync` service implementation. The default implementation of this interface, is the
class called `ExecutorAsync`, and the rest of this file, will be focusing on documenting this implementation,
since it's the default service implementation for Magic Endpoints - Although, technically, you could exchange
this with your own implementation if you wish, completely changing the behaviour of the library.

The controller itself will be invoked for all URLs starting with _"magic/"_, for the following verbs.

* `GET`
* `POST`
* `PUT`
* `DELETE`

The default service implementation, will resolve everything after the _"magic/"_ parts in the
given URL, to a Hyperlambda file that can be found relatively beneath your _"/files/"_ folder.
Although, technically, exactly where you physically put your files on disc, can be configured
through your _"appsettings.json"_ file. The HTTP VERB is assumed to be the last parts of your
filename, before its extension, implying an HTTP GET request such as the following.

```
magic/modules/foo/bar
```

Will resolve to the following physical file on disc.

```
files/modules/foo/bar.get.hl
```

Notice, only the _"magic"_ part in the URL is rewritten, before the verb is appended to the URL, and
the extension _".hl"_ appended. Then this file is loaded and parsed as Hyperlambda, and whatever arguments
you pass in, either as query parameters, or as JSON payload, is appended into your resulting lambda
node's **[.arguments]** node as arguments to your invocation.

**Notice** - Only `PUT` and `POST` can handle JSON payloads, `GET` and `DELETE` can _only_ handle
query parameter arguments. However, all 4 verbs can handle query parameters.

The default implementation can explicitly declare what arguments the file can legally accept, and
if an argument is given during invocation that the file doesn't allow for, an exception will be
thrown, and the file will never be executed. This allows you to declare what arguments your
Hyperlambda file can accept, and avoid having anything _but_ arguments explicitly declared in your
file from being sent into your endpoint file during execution.

An example Hyperlambda file declaring a dynamic Hyperlambda endpoint can be found below.

```
.arguments
   arg1:string
   arg2:int
strings.concat
   get-value:x:@.arguments/*/arg1
   .:" - "
   get-value:x:@.arguments/*/arg2
unwrap:x:+/*
return
   result:x:@strings.concat
```

If you save this file on disc as `/files/modules/magic/foo.get.hl`, you can invoke it as follows, using
the HTTP GET verb.

```
http://localhost:55247/magic/modules/magic/foo?arg1=howdy&arg2=5
```

Assuming you're backend is running on localhost, at port 55247 of course.

## Meta information

Due to the semantic structure of Hyperlambda, retrieving meta information from your HTTP endpoints
using this module is very easy. The project has one slot called **[endpoints.list]** that does this.
This slot again can be invoked using the following URL.

```
http://localhost:55247/magic/modules/system/endpoints/endpoints
```

This endpoint/slot will semantically traverse your endpoints, recursively loading up all Hyperlambda
files from disc, that are resolved from a valid URL, and return meta information about the file/endpoint
back to the caller. This allows the system to easily figure out things such as the following about
your endpoints.

* What is the endpoint's HTTP VERB
* What is the endpoint's URL
* What arguments can the endpoint handle
* Has the file been given a friendly description, through a **[.description]** node
* Etc ...

This slot/endpoint is what allows you to see meta information about all your HTTP REST endpoints
in the _"Endpoints"_ menu item in the Magic Dashboard for instance. The return value from this
slot/endpoint again, is what's used as some sort of frontend is being generated using the Magic
Dashboard.

### Extending the meta data retrieval process

If you wish, you can extend the meta data retrieval process, by
invoking `ListEndpoints.AddMetaDataResolver`. This class can be found in the `magic.endpoint.services.slots`
namespace.

The `AddMetaDataResolver` method takes one function object, which will be invoked for every file
the meta generator is trying to create meta data for, with the complete `lambda`, `verb` and `args`
of your endpoint. This allows you to semantically traverse the lambda/args nodes, and append
any amount of (additional) meta information you wish - Allowing you to extend the generating
of meta data, if you have some sort of general custom Hyperlambda module, creating custom
HTTP endpoints of some sort.

**Notice** - The function will be invoked for _every_ single Hyperlambda file in your system,
every time meta data is retrieved, so you might want to ensure it executes in a fairly short
amount of time, not clogging the server or HTTP endpoint meta generating process in any ways.

## Additional slots

In addition to the meta retrieval endpoint described above, the module contains the following
slots.

** __[http.response.headers.add]__ - Adds an HTTP header to the response object.
** __[http.response.status-code.set]__ - Sets the status code (e.g. 404) on the response object.
