#!/usr/bin/python2

class Date(object):
    def __init__(self, year, day):
        self.year = year
        self.day = day
    def __cmp__(self, other):
        if not isinstance(other, Date):
            return NotImplemented
        return cmp((self.year, self.day), (other.year, other.day))
    def __str__(self):
        return 'y%02dd%03d' % (self.year, self.day)
    @property
    def dict(self):
        return {'year': self.year, 'day': self.day}

ZERO_DATE = Date(0, 0) # game starts on Date(1, 1)

class Contract(object):
    F_UNKNOWN    = 'unknown'
    F_NOT_FIRST  = 'not first'
    F_WAS_LEADER = 'was leader'
    F_FIRST      = 'first'
    def __init__(self, name):
        self.name = name
        self.date = {}
        self.results = {}
    def complete(self, player, date):
        self.date[player] = date
    @property
    def firstdate(self):
        if self.date:
            return min(self.date.values())
    def update(self, mindate):
        if not self.date:
            # no-one's completed it yet (so why were we called?)
            return
        fd = self.firstdate
        if mindate < fd:
            # status is still unknown (again, why were we called?)
            return
        for player in self.date:
            if self.date[player] != fd:
                self.results[player] = self.F_NOT_FIRST
            elif player.leader:
                self.results[player] = self.F_WAS_LEADER
            else:
                self.results[player] = self.F_FIRST
        # return list of new leaders
        return [p for p,r in self.results.items() if r != self.F_NOT_FIRST]
    def first(self, player):
        if player not in self.date:
            # shouldn't happen
            return self.F_UNKNOWN
        if self.results:
            return self.results.get(player, self.F_NOT_FIRST)
        return self.F_UNKNOWN
    def __str__(self):
        return self.name
    @property
    def dict(self):
        return dict((p.name, {'date': self.date[p].dict,
                              'first': self.first(p)})
                    for p in self.date)

class Player(object):
    def __init__(self, name):
        self.name = name
        self.date = ZERO_DATE
        self.leader = False
    def sync(self, date):
        self.date = max(self.date, date)
    def __str__(self):
        return self.name
    @property
    def dict(self):
        return {'date': self.date.dict, 'leader': self.leader}

class Game(object):
    def __init__(self):
        self.players = {}
        self.contracts = {}
        self.oldmindate = ZERO_DATE
    @property
    def mindate(self):
        if not self.players:
            return None
        return min(player.date for player in self.players.values())
    def join(self, player):
        assert player not in self.players, player
        self.players[player] = Player(player)
    def update(self):
        if self.mindate <= self.oldmindate:
            return
        new = [(contract.firstdate, contract)
               for contract in self.contracts.values()
               if self.oldmindate < contract.firstdate and
                  contract.firstdate <= self.mindate]
        for (d,c) in sorted(new):
            leaders = c.update(d)
            for p in self.players.values():
                p.leader = p in leaders
    def sync(self, player, date):
        if player and date:
            assert player in self.players, player
            self.players[player].sync(date)
            self.update()
        return dict((p.name, p.dict) for p in self.players.values())
    def complete(self, contract, player, date):
        assert player in self.players, player
        player = self.players[player]
        # Implicit sync
        player.sync(date)
        # If date < player.date, that means we already sync'd a future date.
        # To avoid breakage, we use the sync date rather than the date supplied
        # with the completion message.
        date = player.date
        if contract not in self.contracts:
            self.contracts[contract] = Contract(contract)
        self.contracts[contract].complete(player, date)
        self.update()
    def results(self, contract):
        if contract not in self.contracts:
            return {}
        return self.contracts[contract].dict

def test():
    g = Game()
    g.join('P1')
    g.join('P2')
    print g.sync('P1', Date(1, 1))
    print g.sync('P2', Date(1, 2))
    print "P1 FS"
    g.complete('FirstSatellite', 'P1', Date(1, 3))
    print g.results('FirstSatellite')
    print g.sync(None, None)
    print "P2 FS"
    g.complete('FirstSatellite', 'P2', Date(1, 4))
    print g.results('FirstSatellite')
    print g.sync(None, None)
    print "P2 CO"
    g.complete('CrewedOrbit', 'P2', Date(1, 6))
    print g.results('CrewedOrbit')
    print "P1 CO"
    g.complete('CrewedOrbit', 'P1', Date(1, 5))
    print g.results('CrewedOrbit')
    print g.sync(None, None)

if __name__ == '__main__':
    test()
