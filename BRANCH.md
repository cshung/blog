# Branch info
https://andrew-algorithm.blogspot.com is basically down. I backed up the blog post data in XML format, this branch is used to convert that data into newer blog posts.

# Architecture:

## Convert
The convert project read the XML file and generate markdown files. It optionally create or consume data in `data.json` where manual input can be given.
## Server
- The server project implements some limited interaction that allows me to build some UI elements on the generated web page so that I can manually work on the things that are not automatic.
## Index
- The index project implements a simple search engine in C#.

# TODO
TODO for conversion:
- Download images from links
- Tags

The volume of posts dictates we need some search capability, the plan is to have a search engine

TODO for a better search:
- Implement the Query in TypeScript (so that it can be hosted in cloudflare)
- Implement porter stemmer
- Improve markdown parsing
- Make indexing general purpose
- Implement inverse document frequency

TODO for a better site:
- Dark mode
- Better presentation
- Page counter using https://countapi.xyz/

