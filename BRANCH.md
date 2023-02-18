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

TODO for a better search:
- Implement porter stemmer
- Implement inverse document frequency

- Indexing side:
- Improve markdown parsing
- Make indexing general purpose
- Avoid indexing shortcode content

- Query side:
- Implement cosine similarity 
- Show blog title instead of link
- Compress (and potentially chunk and delay load) index 

- Others:
- Page counter using https://countapi.xyz/
- GitHub action to automate
- Document blog construction process

TODO bugs:
- Local mermaid display failure

Info:
- https://retrolog.io/blog/creating-a-hugo-theme-from-scratch/
- https://css-tricks.com/a-complete-guide-to-dark-mode-on-the-web/
- https://stackoverflow.com/questions/71799083/white-flash-on-dark-mode-on-refreshing-page
- https://github.com/googlearchive/code-prettify
- https://gohugo.io/content-management/syntax-highlighting/