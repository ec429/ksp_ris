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

# We cannot import errno, because the errno values aren't the same on all
# platforms; we standardise on the Linux values for RIS protocol purposes
ENOENT = 2
EEXIST = 17
EINVAL = 22

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
        e = self.validate(**request.args)
        if e:
            return self.error(request, e)
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
    def validate(self, **kwargs):
        return
    def error(self, request, msg, code=None):
        if request.args.get('json'):
            request.setHeader("content-type", "application/json")
            d = {'err': msg}
            if code is not None:
                d['code'] = code
            return json.dumps(d)
        page = t.html[t.head[t.title['KSP Race Into Space server'],
                             t.link(rel='stylesheet', href='main.css')],
                      t.body[t.h1["Error"],
                             t.h2[msg]]]
        request.setHeader("content-type", "text/html")
        return flatten(page)
    def query_string(self, **kwargs):
        return '?' + '&'.join('%s=%s' % (k, urllib.quote_plus(v))
                              for k,v in kwargs.items())

class Index(Page):
    def data(self, **kwargs):
        return dict((n,{'players': list(games[n].players.keys()),
                        'mindate': games[n].mindate.dict})
                    for n in games)
    def content(self, **kwargs):
        yield t.h1["KSP Race Into Space server"]
        yield t.h2["Games in progress"]
        header = t.tr[t.th["Name"], t.th["Players"], t.th["Min. Date"]]
        rows = [t.tr[t.td[n], t.td[", ".join(games[n].players.keys())],
                     t.td[str(games[n].mindate)]]
                for n in sorted(games)]
        rows.append(t.form(method='GET', action='/newgame')[t.tr[
                        t.td[t.input(type='text', name='name')],
                        t.td(colspan=2)[t.input(type='submit', value='New')]]])
        yield t.table[header, rows]

class ActionFailed(Exception):
    def __init__(self, msg, code=None):
        self.msg = msg
        self.code = code

class Action(Page):
    def render_GET(self, request):
        self.flatten_args(request)
        try:
            dest = self.act(**request.args)
        except ActionFailed as e:
            return self.error(request, e.msg, e.code)
        except Exception as e:
            return self.error(request, str(e))
        request.redirect(dest)
        return ''

class NewGame(Action):
    def act(self, **kwargs):
        name = kwargs.get('name')
        if not name:
            raise ActionFailed("No name specified for new game.", EINVAL)
        if name in games:
            raise ActionFailed("There is already a game named '%s'." % (name,),
                            EEXIST)
        games[name] = ris.Game()
        return '/game' + self.query_string(name=name)

class Game(Page):
    def validate(self, **kwargs):
        name = kwargs.get('name')
        if not name:
            return "No name specified."
        if name not in games:
            return "No such game '%s'." % (name,)
    def data(self, name, **kwargs):
        game = games[name]
        return {'mindate': game.mindate.dict,
                'players': dict((n,game.players[n].dict) for n in game.players.keys())}
    def content(self, name, **kwargs):
        game = games[name]
        yield t.h1["Game: ", name]
        yield t.h2["Min. Date: ", str(game.mindate)]
        yield t.h2["Players"]
        header = t.tr[t.th["Name"], t.th["Date"], t.th]
        rows = [t.tr[t.td[n], t.td[str(game.players[n].date)],
                     t.td['Leader' if game.players[n].leader else []]]
                for n in sorted(game.players)]
        rows.append(t.form(method='GET', action='/join')[
                t.input(type='hidden', name='game', value=name),
                t.tr[t.td[t.input(type='text', name='name')],
                     t.td(colspan=2)[t.input(type='submit', value='New')]]])
        yield t.table[header, rows]

class Join(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        name = kwargs.get('name')
        if not name:
            raise ActionFailed("No player name specified.", EINVAL)
        if name in game.players:
            raise ActionFailed("There is already a player named '%s'." % (name,),
                            EEXIST)
        game.join(name)
        return '/game' + self.query_string(name=gname)

root = resource.Resource()
root.putChild('', Index())
root.putChild('index.htm', Index())
root.putChild('main.css', static.Data(main_css, 'text/css'))
root.putChild('newgame', NewGame())
root.putChild('game', Game())
root.putChild('join', Join())

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
