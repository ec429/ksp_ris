#!/usr/bin/python2
from nevow import tags as t
from nevow.flat import flatten
from twisted.web import server, resource, static
from twisted.internet import reactor, endpoints
import optparse
import json
import urllib
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
    
    def flatten_args(self, request):
        for k in request.args.keys():
            v = request.args[k]
            if isinstance(v, list):
                l = len(v)
                if l == 1:
                    request.args[k] = v[0]
                elif not l:
                    del request.args[k]
    def render_GET(self, request):
        self.flatten_args(request)
        if request.args.get('json'):
            d = self.data(**request.args)
            request.setHeader("content-type", "application/json")
            return json.dumps(d)
        page = t.html[t.head[t.title['KSP Race Into Space server'],
                             t.link(rel='stylesheet', href='main.css')],
                      t.body[self.content(**request.args)]]
        request.setHeader("content-type", "text/html")
        return flatten(page)
    def content(self, **kwargs):
        """Subclasses should probably override this with something prettier."""
        return t.pre(pprint.pformat(self.data(**kwargs)))
    def error(self, request, msg):
        if request.args.get('json'):
            request.setHeader("content-type", "application/json")
            return json.dumps({'err': msg})
        page = t.html[t.head[t.title['KSP Race Into Space server'],
                             t.link(rel='stylesheet', href='main.css')],
                      t.body[t.h1["Error"],
                             t.h2[msg]]]
        request.setHeader("content-type", "text/html")
        return flatten(page)

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
                for n in sorted(games)]
        rows.append(t.form(method='GET', action='/newgame')[t.tr[
                        t.td[t.input(type='text', name='name')],
                        t.td(colspan=2)[t.input(type='submit', value='New')]]])
        yield t.table[header, rows]

class NewGame(Page):
    def render_GET(self, request):
        self.flatten_args(request)
        name = request.args.get('name')
        if not name:
            return self.error(request, "No name specified for new game.")
        if name in games:
            return self.error(request,
                              "There is already a game named '%s'." % (name,))
        games[name] = ris.Game()
        request.redirect('/game?' + urllib.quote_plus(name))
        return ''

root = resource.Resource()
root.putChild('', Index())
root.putChild('index.htm', Index())
root.putChild('main.css', static.Data(main_css, 'text/css'))
root.putChild('newgame', NewGame())

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
