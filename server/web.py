#!/usr/bin/python2
from nevow import tags as t
from nevow.flat import flatten
from twisted.web import server, resource, static
from twisted.internet import reactor, endpoints
import optparse
import json
import pprint

import ris

main_css = """
table { border: 1px solid; }
th,td { border: 1px solid gray; }
"""

games = {}

class Page(resource.Resource):
    """Abstract base class for pages with both data and human-readable forms."""
    isLeaf = True
    
    def render_GET(self, request):
        request.setHeader("content-type", "text/html")
        if request.args.get('json'):
            return json.dumps(self.data(**request.args))
        page = t.html[t.head[t.title['KSP Race Into Space server'],
                             t.link(rel='stylesheet', href='main.css')],
                      t.body[self.content(**request.args)]]
        return flatten(page)
    def content(self, **kwargs):
        """Subclasses should probably override this with something prettier."""
        return t.pre(pprint.pformat(self.data(**kwargs)))

class Index(Page):
    def data(self, **kwargs):
        return dict((n,{'players': list(games[n].players.keys()),
                        'mindate': games[n].mindate.dict})
                    for n in games)
    def content(self, **kwargs):
        yield t.h1["KSP Race Into Space server"]
        yield t.h2["Games in progress"]
        header = t.tr[t.th["Name"], t.th["Players"], t.th["Min. Date"]]
        rows = [[t.td[n], t.td[", ".join(games[n].players.keys())],
                 t.td[str(games[n].mindate)]]
                for n in games]
        rows.append(t.form(method='GET', action='/newgame')[t.tr[
                        t.td[t.input(type='text', name='name')],
                        t.td(colspan=2)[t.input(type='submit', value='New')]]])
        yield t.table[header, rows]

root = resource.Resource()
root.putChild('', Index())
root.putChild('index.htm', Index())
root.putChild('main.css', static.Data(main_css, 'text/css'))

def parse_args():
    x = optparse.OptionParser()
    x.add_option('-p', '--port', type='int', help='TCP port number to serve',
                 default=8080)
    opts, args = x.parse_args()
    if args:
        x.error("Unexpected positional arguments")
    return opts

def main(opts):
    ep = "tcp:%d"%(opts.port,)
    endpoints.serverFromString(reactor, ep).listen(server.Site(root))
    reactor.run()

if __name__ == '__main__':
    main(parse_args())
