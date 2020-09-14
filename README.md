
# Magic Endpoint

[![Build status](https://travis-ci.com/polterguy/magic.endpoint.svg?master)](https://travis-ci.com/polterguy/magic.endpoint)

Magic Endpoint is a dynamic endpoint URL controller, allowing you to declare endpoints that are dynamically
resolved using your `IExecutorAsync` service implementation. The default implementation of this interface, is the
class called `ExecutorAsync`, and the rest of this file, will be focusing on documenting this implementation,
since it's the default service implementation for Magic Endpoint - Although, technically, you could exchange
this with your own implementation if you wish, completely changing the behaviour of the library.

The controller itself will be invoked for all URLs starting with _"magic/"_, for the following verbs.

* `GET`
* `POST`
* `PUT`
* `DELETE`

The default service implementation, will resolve everything after the _"magic/"_ parts in the
given URL, to a Hyperlambda file that can be found relatively beneath your _"files/"_ folder.
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
finally the extension _".hl"_ appended. Then the file is loaded and parsed as Hyperlambda, and whatever
arguments you pass in, either as query parameters, or as JSON payload, is appended into your resulting
lambda node's **[.arguments]** node, as arguments to your invocation.

**Notice** - Only `PUT` and `POST` can handle JSON payloads. All 4 verbs can handle query parameters
though. Below is probably the simplest HTTP endpoint you could create. Save the following Hyperlambda in a
file at the path of `modules/magic/foo1.get.hl` using for instance your Magic Dashboard's
_"Files"_ menu item.

```
return
   result:Hello from Magic Backend
```

Then invoke the endpoint using the following URL.

```
http://localhost:55247/magic/modules/magic/foo1
```

## Arguments

The default implementation can explicitly declare what arguments the file can legally accept, and
if an argument is given during invocation that the file doesn't allow for, an exception will be
thrown, and the file will never be executed. This allows you to declare what arguments your
Hyperlambda file can accept, and avoid having anything _but_ arguments explicitly declared in your
Hyperlambda file from being sent into your endpoint during invocation of your HTTP endpoint.

An example Hyperlambda file taking two arguments can be found below.

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

If you save this file on disc as `/files/modules/magic/foo2.get.hl`, you can invoke it as follows, using
the HTTP GET verb.

```
http://localhost:55247/magic/modules/magic/foo2?arg1=howdy&arg2=5
```

Assuming you're backend is running on localhost, at port 55247 of course.

JSON payloads are automatically converted to lambda/nodes - And query parameters are treated
indiscriminately the same way as JSON payloads - Except of course, query parameters cannot
pass in complex graph objects, but only simply key/value arguments.

**Notice** - To allow for _any_ arguments to your files, simply _ommit_ the **[.arguments]** node
in your Hyperlambda althogether. Alternatively, you can also partially ignore arguments sanity checking
of individual nodes, by setting their values to `*`, such as the following illustrates.

```
.arguments
   arg1:string
   arg2:date
   arg3:*
```

In the above arguments declaration, **[arg1]** and **[arg2]** will be sanity checked, and input converted
to `string` or `date` (DateTime) - But the **[arg3]** parts will be completely ignored, allowing the caller
to invoke it with _anything_ as `arg3` during invocation - Including complete graph JSON objects, assuming
the above declaration is for a `PUT` or `POST` Hyperlambda file.

To declare what type your arguments can be, set the value of the argument declaration node to
the Hyperlambda type value inside of your arguments declaration, such as we illustrate above.
Arguments will be converted, if possible, to the typ declaration in your arguments declaration.
If no conversion is possible, an exception will be thrown.

Although the sanity check will check graph objects, passed in as JSON payloads, it has its restrictions,
such as not being able to sanity checking complex objects passed in as arrays, etc. If you need stronger
sanity checking of your arguments, you will probably have to manually check your more complex graph objects
yourself.

## Meta information

Due to the semantic structure of Hyperlambda, retrieving meta information from your HTTP endpoints
using this module is very easy. The project has one slot called **[endpoints.list]** that returns
meta information about _all_ your endpoints. This slot again can be invoked using the following URL.

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
invoking `ListEndpoints.AddMetaDataResolver`, and pass in your own function. This class can be
found in the `magic.endpoint.services.slots` namespace.

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

* __[http.response.headers.add]__ - Adds an HTTP header to the response object.
* __[http.response.status-code.set]__ - Sets the status code (e.g. 404) on the response object.

## Misc

Unless you explicitly change the `Content-Type` of your response object, by using
the **[http.response.headers.add]** slot, a Content-Type of `application/json` will be assumed,
and this header will be appended into the resulting HTTP response object. To return plain
text for instance, you could create an endpoint containing the following.

```
http.response.headers.add
   Content-Type:text/plain
return:Hello from Magic Backend
```

You can also return stream objects using for instance the **[return]** slot, at which point
ASP.NET Core will automatically stream your content back over the response object, and `Dispose`
your stream automatically for you afterwards. This allows you to return large files back to
the client, without loading them into memory first, etc.

Below is a link for you if you want to further your understanding of Hyperlambda, in order
to understand what you can put _into_ your Magic Endpoint files.

* [Magic Lambda](https://github.com/polterguy/magic.lambda)

## Quality gates

- [![Build status](https://travis-ci.com/polterguy/magic.endpoint.svg?master)](https://travis-ci.com/polterguy/magic.endpoint)
- [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=alert_status)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=bugs)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=code_smells)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=coverage)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=duplicated_lines_density)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=ncloc)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=security_rating)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.http&metric=sqale_index)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
- [![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polterguy_magic.endpoint&metric=vulnerabilities)](https://sonarcloud.io/dashboard?id=polterguy_magic.endpoint)
