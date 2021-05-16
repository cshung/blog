# Branch info
My blogspot account is basically useless now. I downloaded the XML file and is planning to convert them into markdown file and serve the pages here.

The converter is a semi automatic HTML to markdown converter, it is somewhat broken, and it is designed that way.

The goal is *NOT* to create a perfect converter, the goal is to convert most of the things that could be done automatically and handle the rest manually.

TODO for conversion:
- Download images from links
- Convert links to blogspot to myself
- Tags
- Have a mechanism to make progress for semi-automatic conversion, there is no need to repeatedly inspect completed work.

The volume of posts dictates we need some search capability, the plan is to index the pages locally and use a cloudflare worker to perform the search.

Opportunities to make search better:
- Implement porter stemmer
- Improve markdown parsing
- Make indexing general purpose
- Implement the Query in TypeScript (so that it can be hosted in cloudflare)
- Implement inverse document frequency

TODO for a better site:
- Better presentation
- Page counter using https://countapi.xyz/