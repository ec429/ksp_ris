#!/usr/bin/python2
from nevow import tags as t
from nevow.flat import flatten
from twisted.web import server, resource, static
from twisted.internet import reactor, endpoints
import optparse
import json
import urllib
import pprint
import os

import ris

# We cannot import errno, because the errno values aren't the same on all
# platforms; we standardise on the Linux values for RIS protocol purposes
EPERM = 1
ENOENT = 2
EEXIST = 17
EINVAL = 22
ENOTEMPTY = 39

main_css = """
table { border: 1px solid; }
th,td { border: 1px solid gray; }
td.num { text-align: right; }
"""

games = {}

class Failed(Exception):
    def __init__(self, msg, code=None):
        self.msg = msg
        self.code = code

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
        request.args['_local'] = request.getClientIP() == '127.0.0.1'
    def render_GET(self, request):
        self.flatten_args(request)
        try:
            self.validate(**request.args)
        except Failed as e:
            return self.error(request, e.msg, e.code)
        except Exception as e:
            return self.error(request, repr(e))
        if request.args.get('json'):
            try:
                d = self.data(**request.args)
                request.setHeader("content-type", "application/json")
                return json.dumps(d)
            except Exception as e:
                return self.error(request, repr(e))
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
                              for k,v in kwargs.items() if v is not None)

class Index(Page):
    def data(self, **kwargs):
        return dict((n,{'players': list(games[n].players.keys()),
                        'mindate': games[n].mindate.dict})
                    for n in games)
    def content(self, **kwargs):
        yield t.h1["KSP Race Into Space server"]
        yield t.h2["Games in progress"]
        header = t.tr[t.th["Name"], t.th["Players"], t.th["Min. Date"]]
        rows = [t.form(method='GET', action='/rmgame')[t.tr[
                    t.td[t.a(href="/game" + self.query_string(name=n))[n]],
                    t.td[", ".join(games[n].players.keys())],
                    t.td[str(games[n].mindate)],
                    t.td if games[n].locked or not kwargs.get('_local') else
                    t.td[t.input(type='hidden', name='game', value=n),
                         t.input(type='submit', value='End')]
                    ]]
                for n in sorted(games)]
        rows.append(t.form(method='GET', action='/newgame')[t.tr[
                        t.td[t.input(type='text', name='name')],
                        t.td(colspan=2),
                        t.td[t.input(type='submit', value='New')]]])
        yield t.table[header, rows]

class ActionFailed(Failed): pass

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
        if '/' in name:
            raise ActionFailed("Game name may not contain '/'.", EINVAL)
        games[name] = ris.Game(name)
        games[name].save()
        return '/game' + self.query_string(name=name, json=kwargs.get('json'))

class RmGame(Action):
    def act(self, **kwargs):
        name = kwargs.get('game')
        if not name:
            raise ActionFailed("No game specified.", EINVAL)
        if name not in games:
            raise ActionFailed("There is no game named '%s'." % (name,), ENOENT)
        game = games[name]
        if game.locked or not kwargs.get('_local'):
            raise ActionFailed("Game is locked.", EPERM)
        if game.players:
            raise ActionFailed("Game '%s' has %d players." % (name, len(game.players)),
                               ENOTEMPTY)
        game.rm()
        del games[name]
        return '/' + self.query_string(json=kwargs.get('json'))

class Game(Page):
    def validate(self, **kwargs):
        name = kwargs.get('name')
        if not name:
            raise Failed("No name specified.", EINVAL)
        if name not in games:
            raise Failed("No such game '%s'." % (name,), ENOENT)
    def data(self, name, **kwargs):
        return games[name].dict
    def content(self, name, **kwargs):
        game = games[name]
        admin = kwargs.get('_local') and not game.locked
        yield t.h1["Game: ", name]
        yield t.h2["Min. Date: ", str(game.mindate)]
        yield t.h2["Players"]
        header = t.tr[t.th["Name"], t.th["Date"], t.th["Cem'y"], t.th,
                      [] if game.locked else t.th]
        rows = [t.form(method='GET', action='/part')[t.tr[
                     t.td[t.a(href="/player" +
                              self.query_string(game=name, name=n)
                              )[n]],
                     t.td[str(game.players[n].date)],
                     t.td(Class='num')[str(game.players[n].kia)],
                     t.td['Leader' if game.players[n].leader else []],
                     [] if not admin else
                     t.td[t.input(type='hidden', name='game', value=name),
                          t.input(type='hidden', name='name', value=n),
                          t.input(type='submit', value='Remove')],
                     ]]
                for n in sorted(game.players)]
        rows.append(t.form(method='GET', action='/join')[
                t.input(type='hidden', name='game', value=name),
                t.tr[t.td[t.input(type='text', name='name')],
                     t.td(colspan=2),
                     [] if not admin else
                     t.td[t.input(type='submit', value='New')]]])
        if admin:
            yield t.form(method='GET', action='/lock')[
                    t.input(type='hidden', name='game', value=name),
                    t.input(type='submit', value='Lock')]
        yield t.table[header, rows]
        def shortresult(contract):
            front = [p for p in contract.date
                     if contract.date[p] == contract.firstdate]
            text = ['%s, %s, %s'%(p.name, contract.date[p], contract.first(p))
                    for p in front]
            return '(%s)'%('; '.join(text))
        yield t.h2["Contracts"]
        yield t.ul[[t.li[t.a(href="/result" + self.query_string(game=name,
                                                                contract=n))[n],
                         ': ', shortresult(game.contracts[n])]
                    for n in sorted(game.contracts,
                                    key=lambda n:game.contracts[n].firstdate)]]

class Player(Page):
    def validate(self, **kwargs):
        name = kwargs.get('game')
        if not name:
            raise Failed("No game specified.", EINVAL)
        if name not in games:
            raise Failed("No such game '%s'." % (name,), ENOENT)
        game = games[name]
        pname = kwargs.get('name')
        if not pname:
            raise Failed("No player specified.", EINVAL)
        if pname not in game.players:
            raise Failed("No such player '%s'." % (pname,), ENOENT)
    def data(self, game, name, **kwargs):
        game = games[game]
        player = game.players[name]
        return dict((contract.name,{'date': contract.date[player],
                                    'first': contract.first(player)})
                    for contract in game.contracts.values()
                    if player in contract.date)
    def content(self, game, name, **kwargs):
        game = games[game]
        player = game.players[name]
        yield t.h1["Player: ", player.name]
        yield t.h2["Date: ", str(player.date)]
        if player.kia:
            yield t.h2["%d astronauts K.I.A." % (player.kia,)]
        if player.leader:
            yield t.h2["Has Leader flag"]
        contracts = [c for c in game.contracts.values() if player in c.date]
        header = t.tr[t.th["Contract"], t.th["Date"], t.th["Result"]]
        rows = [t.tr[t.td[t.a(href="/result" +
                              self.query_string(game=game.name, contract=c.name)
                              )[c.name]],
                     t.td[str(c.date[player])],
                     t.td[c.first(player)]]
                for c in sorted(contracts, key=lambda c:c.date[player])]
        yield t.table[header, rows]

class Result(Page):
    def validate(self, **kwargs):
        name = kwargs.get('game')
        if not name:
            raise Failed("No game specified.", EINVAL)
        if name not in games:
            raise Failed("No such game '%s'." % (name,), ENOENT)
        game = games[name]
        cname = kwargs.get('contract')
        if not cname:
            raise Failed("No contract specified.", EINVAL)
        if cname not in game.contracts:
            raise Failed("No such contract '%s'." % (cname,), ENOENT)
    def data(self, game, contract, **kwargs):
        return games[game].contracts[contract].dict
    def content(self, game, contract, **kwargs):
        game = games[game]
        contract = game.contracts[contract]
        yield t.h1["Contract: ", contract.name]
        yield t.h2["Firstdate: ", str(contract.firstdate)]
        yield t.h2["Results"]
        header = t.tr[t.th["Player"], t.th["Date"], t.th["Result"]]
        rows = [t.tr[t.td[t.a(href="/player" +
                              self.query_string(game=game.name, name=p.name)
                              )[p.name]],
                     t.td[str(contract.date[p])],
                     t.td[contract.first(p)]]
                for p in sorted(contract.date, key=lambda p:contract.date[p])]
        yield t.table[header, rows]

class Lock(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        if not kwargs.get('_local'):
            raise ActionFailed("You're not the server administrator.", EPERM)
        game.locked = True
        game.save()
        return '/game' + self.query_string(name=gname, json=kwargs.get('json'))

class Join(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        if game.locked:
            raise ActionFailed("Game is locked.", EPERM)
        name = kwargs.get('name')
        if not name:
            raise ActionFailed("No player name specified.", EINVAL)
        if name in game.players:
            raise ActionFailed("There is already a player named '%s'." % (name,),
                            EEXIST)
        game.join(name)
        game.save()
        return '/game' + self.query_string(name=gname, json=kwargs.get('json'))

class Part(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        if game.locked:
            raise ActionFailed("Game is locked.", EPERM)
        name = kwargs.get('name')
        if not name:
            raise ActionFailed("No player name specified.", EINVAL)
        if name not in game.players:
            raise ActionFailed("There is no player named '%s'." % (name,),
                               ENOENT)
        game.part(name)
        game.save()
        return '/game' + self.query_string(name=gname, json=kwargs.get('json'))

class Sync(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        pname = kwargs.get('player')
        if not pname:
            raise ActionFailed("No player name specified.", EINVAL)
        if pname not in game.players:
            raise ActionFailed("There is no player named '%s'." % (pname,),
                               ENOENT)
        year = kwargs.get('year')
        if not year:
            raise ActionFailed("No year specified.", EINVAL)
        day = kwargs.get('day')
        if not day:
            raise ActionFailed("No day specified.", EINVAL)
        try:
            kia = int(kwargs.get('kia', 0))
        except ValueError:
            raise ActionFailed("Bad 'kia' value '%s'." % (kwargs['kia'],),
                               EINVAL)
        game.sync(pname, ris.Date(int(year), int(day)), kia=kia)
        game.save()
        return '/game' + self.query_string(name=gname, json=kwargs.get('json'))

class Completed(Action):
    def act(self, **kwargs):
        gname = kwargs.get('game')
        if not gname:
            raise ActionFailed("No game name specified.", EINVAL)
        if gname not in games:
            raise ActionFailed("No such game '%s'." % (gname,), ENOENT)
        game = games[gname]
        pname = kwargs.get('player')
        if not pname:
            raise ActionFailed("No player name specified.", EINVAL)
        if pname not in game.players:
            raise ActionFailed("There is no player named '%s'." % (pname,),
                               ENOENT)
        year = kwargs.get('year')
        if not year:
            raise ActionFailed("No year specified.", EINVAL)
        day = kwargs.get('day')
        if not day:
            raise ActionFailed("No day specified.", EINVAL)
        cname = kwargs.get('contract')
        if not cname:
            raise ActionFailed("No contract specified.", EINVAL)
        try:
            tier = int(kwargs.get('tier', 0))
        except ValueError:
            raise ActionFailed("Bad 'tier' value '%s'." % (kwargs['tier'],),
                               EINVAL)
        game.complete(cname, pname, ris.Date(int(year), int(day)),
                      tier=tier)
        game.save()
        return '/result' + self.query_string(game=gname, contract=cname,
                                             json=kwargs.get('json'))

root = resource.Resource()
root.putChild('', Index())
root.putChild('index.htm', Index())
root.putChild('main.css', static.Data(main_css, 'text/css'))
root.putChild('newgame', NewGame())
root.putChild('rmgame', RmGame())
root.putChild('game', Game())
root.putChild('lock', Lock())
root.putChild('join', Join())
root.putChild('part', Part())
root.putChild('sync', Sync())
root.putChild('player', Player())
root.putChild('result', Result())
root.putChild('completed', Completed())

def parse_args():
    x = optparse.OptionParser()
    x.add_option('-p', '--port', type='int', help='TCP port number to serve',
                 default=8080)
    x.add_option('-f', '--strict', action='store_true')
    opts, args = x.parse_args()
    if args:
        x.error("Unexpected positional arguments")
    return opts

def load_games(opts):
    rv = {}
    for fn in os.listdir('games'):
        try:
            path = os.path.join('games', fn)
            rv[fn] = ris.Game.load(fn, open(path, 'r'))
        except Exception as e:
            if opts.strict:
                raise
            print "Failed to load %s (skipping): %r" % (fn, e)
    return rv

def main(opts):
    global games
    games = load_games(opts)
    ep = "tcp:%d"%(opts.port,)
    endpoints.serverFromString(reactor, ep).listen(server.Site(root))
    reactor.run()

if __name__ == '__main__':
    main(parse_args())
